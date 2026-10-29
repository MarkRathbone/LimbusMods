"""Read-only PE/export validation. DLL edits are staged by the PowerShell launcher."""
import argparse
import hashlib
import json
import pathlib
import re
import struct
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, str(pathlib.Path(__file__).parent / 'vendor'))
import capstone
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_OP_REG


def sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


class PE:
    def __init__(self, path):
        self.data = pathlib.Path(path).read_bytes()
        b = self.data
        if b[:2] != b'MZ':
            raise ValueError('Not a PE file')
        nt = struct.unpack_from('<I', b, 60)[0]
        if b[nt:nt + 4] != b'PE\0\0' or struct.unpack_from('<H', b, nt + 4)[0] != 0x8664:
            raise ValueError('Expected x64 GameAssembly')
        count = struct.unpack_from('<H', b, nt + 6)[0]
        opt_size = struct.unpack_from('<H', b, nt + 20)[0]
        opt = nt + 24
        if struct.unpack_from('<H', b, opt)[0] != 0x20b:
            raise ValueError('Expected PE32+')
        export, export_size = struct.unpack_from('<II', b, opt + 112)
        self.sections = []
        for i in range(count):
            s = opt + opt_size + i * 40
            virtual_size, va, raw_size, raw = struct.unpack_from('<IIII', b, s + 8)
            self.sections.append((va, max(virtual_size, raw_size), raw, raw_size))
        e = self.read(export, 40)
        _, names, functions, strings, ordinals = struct.unpack_from('<IIIII', e, 20)
        if not 0 < names < 10000:
            raise ValueError('Unexpected export count')
        self.exports = {}
        for i in range(names):
            string_rva = struct.unpack('<I', self.read(strings + 4 * i, 4))[0]
            ordinal = struct.unpack('<H', self.read(ordinals + 2 * i, 2))[0]
            rva = struct.unpack('<I', self.read(functions + 4 * ordinal, 4))[0]
            if export <= rva < export + export_size:
                raise ValueError('Forwarded exports require review')
            name = self.read(string_rva, 256).split(b'\0', 1)[0].decode('ascii')
            if rva:
                self.exports[name] = rva

    def read(self, rva, size):
        for va, length, raw, raw_size in self.sections:
            if va <= rva < va + length:
                offset = rva - va
                if offset + size > raw_size:
                    raise ValueError('PE read exceeds stored section')
                return self.data[raw + offset:raw + offset + size]
        raise ValueError('RVA outside PE sections')


def shape(pe, rva):
    """Retain registers/constants/field offsets; normalize relocation targets only."""
    md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    md.detail = True
    result = []
    for ins in md.disasm(pe.read(rva, 160), rva):
        if ins.mnemonic == 'int3':
            break
        args = []
        for op in ins.operands:
            if op.type == X86_OP_REG:
                args.append(ins.reg_name(op.reg))
            elif op.type == X86_OP_IMM:
                args.append('target' if ins.group(capstone.CS_GRP_CALL) or ins.group(capstone.CS_GRP_JUMP)
                            else str(op.imm))
            elif op.type == X86_OP_MEM:
                base = ins.reg_name(op.mem.base) or ''
                args.append(f'mem:{base}:{ins.reg_name(op.mem.index) or ""}:{op.mem.scale}:'
                            + ('rip' if base == 'rip' else str(op.mem.disp)))
        result.append(ins.mnemonic + ' ' + ','.join(args))
        if ins.mnemonic in ('ret', 'jmp'):
            break
    if not result:
        raise ValueError('Empty export instruction signature')
    return result


def snapshot(pe, imports):
    ranks = sorted(set(pe.exports.values()))
    bindings, optional = {}, []
    for api, name in imports.items():
        if not api.startswith('il2cpp_'):
            continue
        if name not in pe.exports:
            if name != api:
                raise ValueError(f'Loader import {api} -> {name} is missing from GameAssembly')
            optional.append(api)  # Existing unsupported legacy optional imports.
            continue
        rva = pe.exports[name]
        bindings[api] = {'name': name, 'rank': ranks.index(rva), 'shape': shape(pe, rva)}
    if len(bindings) < 150 or 'il2cpp_domain_get' not in bindings or 'il2cpp_runtime_invoke' not in bindings:
        raise ValueError('Incomplete loader baseline')
    return {'uniqueExports': len(ranks), 'bindings': bindings, 'optionalLegacyImports': sorted(optional)}


CUSTOM_ATTRIBUTES = frozenset({
    'il2cpp_custom_attrs_has_attr', 'il2cpp_custom_attrs_get_attr',
    'il2cpp_custom_attrs_construct'})


def attribute_pair(old, new):
    if len(old) != len(new):
        return None
    changed = [i for i, values in enumerate(zip(old, new)) if values[0] != values[1]]
    if not changed or len(changed) > 2:
        return None
    candidates = []
    for i in range(len(old) - 2):
        pairs = []
        for sequence in (old, new):
            load = re.fullmatch(r'mov eax,mem:rax::1:(\d+)', sequence[i])
            arithmetic = re.fullmatch(r'(add|sub) eax,(\d+)', sequence[i + 1])
            if not load or not arithmetic or sequence[i + 2] != 'cdqe ':
                break
            offset, constant = int(load[1]), int(arithmetic[2])
            if not 0 <= offset <= 4096 or offset % 4 or not 0 < constant <= 65535:
                break
            pairs.append((offset, arithmetic[1], constant))
        if len(pairs) == 2 and all(j in (i, i + 1) for j in changed):
            candidates.append(tuple(pairs))
    return candidates[0] if len(candidates) == 1 else None


def engine_fingerprint(path, version):
    pe = PE(path)
    b = pe.data
    nt = struct.unpack_from('<I', b, 60)[0]
    count = struct.unpack_from('<H', b, nt + 6)[0]
    opt_size = struct.unpack_from('<H', b, nt + 20)[0]
    sections = []
    for i in range(count):
        s = nt + 24 + opt_size + i * 40
        flags = struct.unpack_from('<I', b, s + 36)[0]
        if flags & 0x20000000:
            virtual_size, va, size, raw = struct.unpack_from('<IIII', b, s + 8)
            if not size or raw + size > len(b):
                raise ValueError('Invalid engine executable section')
            sections.append({'name': b[s:s+8].rstrip(b'\0').decode('ascii'),
                             'rva': va, 'virtualSize': virtual_size, 'size': size,
                             'sha256': hashlib.sha256(b[raw:raw+size]).hexdigest()})
    if not version or not sections:
        raise ValueError('Engine version and executable sections are required')
    return {'version': version, 'executableSections': sections}


def validate_engine(state, current):
    if current['unityPlayerSha256'] == state['unityPlayerSha256']:
        return False
    old = state.get('engineFingerprint')
    if not old or old != current['engineFingerprint']:
        raise ValueError('Unity engine version/code changed; manual compatibility review required')
    return True


def remap(baseline, pe, accepted_changes=None):
    ranks = sorted(set(pe.exports.values()))
    if len(ranks) != baseline['uniqueExports']:
        raise ValueError('Export layout changed: manual compatibility review required')
    names = {}
    for name, rva in pe.exports.items():
        names.setdefault(rva, []).append(name)
    mapping, failures, attribute_changes = {}, [], {}
    for api, binding in baseline['bindings'].items():
        rva = ranks[binding['rank']]
        current_shape = shape(pe, rva)
        if current_shape != binding['shape']:
            pair = attribute_pair(binding['shape'], current_shape) if api in CUSTOM_ATTRIBUTES else None
            if pair is None:
                failures.append(api)
                continue
            attribute_changes[api] = pair
        aliases = sorted(names[rva])
        mapping[api] = next((n for n in aliases if len(n) == 11), aliases[0])
    if attribute_changes:
        if set(attribute_changes) != CUSTOM_ATTRIBUTES or len(set(attribute_changes.values())) != 1:
            failures.extend(attribute_changes)
        elif accepted_changes is not None:
            accepted_changes.extend(sorted(attribute_changes))
    if failures:
        raise ValueError('API instruction signatures changed; no repair applied: ' + ', '.join(failures))
    return mapping


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['initialize', 'plan'])
    parser.add_argument('--game', required=True)
    parser.add_argument('--imports', required=True)
    parser.add_argument('--state', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--build', default='unknown')
    parser.add_argument('--engine-version', required=True)
    args = parser.parse_args()
    game = pathlib.Path(args.game)
    ga = game / 'GameAssembly.dll'
    imports = json.loads(pathlib.Path(args.imports).read_text(encoding='utf-8-sig'))
    pe = PE(ga)
    hashes = {n: sha256(game / 'BepInEx/core' / n)
              for n in ['Il2CppInterop.Runtime.dll', 'BepInEx.Unity.IL2CPP.dll']}
    current = {'gameAssemblySha256': sha256(ga), 'unityPlayerSha256': sha256(game / 'UnityPlayer.dll'),
               'steamBuildId': args.build, 'coreHashes': hashes,
               'engineFingerprint': engine_fingerprint(game / 'UnityPlayer.dll', args.engine_version)}
    if args.action == 'initialize':
        output = {'schema': 1, **current, 'exports': snapshot(pe, imports), 'startupVerified': False}
    else:
        state = json.loads(pathlib.Path(args.state).read_text(encoding='utf-8-sig'))
        if state.get('schema') != 1:
            raise ValueError('Unsupported baseline schema')
        if hashes != state['coreHashes']:
            raise ValueError('Loader files changed outside this launcher; review before reinitializing')
        engine_metadata_changed = validate_engine(state, current)
        accepted_changes = []
        unchanged = current['gameAssemblySha256'] == state['gameAssemblySha256']
        mapping = {api: b['name'] for api, b in state['exports']['bindings'].items()} if unchanged else remap(state['exports'], pe, accepted_changes)
        for api, name in mapping.items():
            if imports.get(api) != state['exports']['bindings'][api]['name']:
                raise ValueError('Installed bindings differ from baseline: ' + api)
        output = {'status': 'unchanged' if unchanged and not engine_metadata_changed else 'repair',
                  'mapping': mapping, 'acceptedAttributeChanges': accepted_changes,
                  'engineMetadataChanged': engine_metadata_changed, **current}
    pathlib.Path(args.output).write_text(json.dumps(output, indent=2), encoding='utf8')


if __name__ == '__main__':
    try:
        main()
    except Exception as ex:
        print('Compatibility check stopped: ' + str(ex), file=sys.stderr)
        sys.exit(2)
