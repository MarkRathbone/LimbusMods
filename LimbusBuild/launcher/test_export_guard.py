import unittest
from unittest.mock import patch
from export_guard import PE, remap, shape, attribute_pair, CUSTOM_ATTRIBUTES, validate_engine


class FakePE:
    def __init__(self, exports, code=None):
        self.exports = exports
        self.code = code or {}

    def read(self, rva, size):
        return self.code.get(rva, b'\xc3') + b'\xcc' * (size - len(self.code.get(rva, b'\xc3')))


class GuardTests(unittest.TestCase):
    def baseline(self):
        return {'uniqueExports': 2, 'bindings': {
            'il2cpp_domain_get': {'rank': 0, 'shape': ['domain']},
            'il2cpp_runtime_invoke': {'rank': 1, 'shape': ['invoke']}}}

    def test_renames_and_shifted_addresses(self):
        pe = FakePE({'NewDomainAB': 200, 'NewInvokeAB': 400})
        with patch('export_guard.shape', side_effect=lambda p, r: ['domain'] if r == 200 else ['invoke']):
            self.assertEqual(remap(self.baseline(), pe), {
                'il2cpp_domain_get': 'NewDomainAB', 'il2cpp_runtime_invoke': 'NewInvokeAB'})

    def test_changed_export_count_stops(self):
        with self.assertRaisesRegex(ValueError, 'layout changed'):
            remap(self.baseline(), FakePE({'one': 200}))

    def test_changed_instruction_signature_stops(self):
        with patch('export_guard.shape', return_value=['wrong ABI']):
            with self.assertRaisesRegex(ValueError, 'signatures changed'):
                remap(self.baseline(), FakePE({'one': 200, 'two': 400}))

    def test_same_address_aliases_do_not_shift_ranks(self):
        pe = FakePE({'aliasA': 200, 'aliasB': 200, 'invoke': 400})
        with patch('export_guard.shape', side_effect=lambda p, r: ['domain'] if r == 200 else ['invoke']):
            result = remap(self.baseline(), pe)
            self.assertEqual(pe.exports[result['il2cpp_domain_get']], 200)
            self.assertEqual(result['il2cpp_runtime_invoke'], 'invoke')

    def test_branch_relocation_normalized(self):
        old = FakePE({}, {100: b'\xe9\x20\x00\x00\x00'})
        new = FakePE({}, {300: b'\xe9\x70\x00\x00\x00'})
        self.assertEqual(shape(old, 100), shape(new, 300))

    def test_field_layout_changes_are_not_ignored(self):
        old = FakePE({}, {100: b'\x48\x8b\x41\x18\xc3'})
        new = FakePE({}, {300: b'\x48\x8b\x41\x20\xc3'})
        self.assertNotEqual(shape(old, 100), shape(new, 300))

    def test_invalid_pe_rejected(self):
        with patch('pathlib.Path.read_bytes', return_value=b'not a PE'):
            with self.assertRaisesRegex(ValueError, 'Not a PE'):
                PE('bad.dll')


class WeeklyUpdateTests(unittest.TestCase):
    old = ['prefix', 'mov eax,mem:rax::1:776', 'sub eax,7928', 'cdqe ', 'suffix']
    new = ['prefix', 'mov eax,mem:rax::1:872', 'sub eax,6284', 'cdqe ', 'suffix']

    def test_recognized_pair(self):
        self.assertIsNotNone(attribute_pair(self.old, self.new))

    def test_previous_add_to_sub_pattern(self):
        old = self.old.copy(); old[1:3] = ['mov eax,mem:rax::1:592', 'add eax,5500']
        self.assertIsNotNone(attribute_pair(old, self.new))

    def test_other_instructions_must_match(self):
        new = self.new.copy(); new[-1] = 'changed'
        self.assertIsNone(attribute_pair(self.old, new))

    def test_invalid_offsets_and_constants_rejected(self):
        for instruction in ['mov eax,mem:rax::1:873', 'mov eax,mem:rax::1:99999']:
            new = self.new.copy(); new[1] = instruction
            self.assertIsNone(attribute_pair(self.old, new))
        new = self.new.copy(); new[2] = 'sub eax,999999'
        self.assertIsNone(attribute_pair(self.old, new))

    def run_remap(self, changes):
        apis = sorted(CUSTOM_ATTRIBUTES) + ['il2cpp_domain_get']
        baseline = {'uniqueExports': 4, 'bindings': {
            api: {'rank': i, 'shape': self.old if api in CUSTOM_ATTRIBUTES else ['domain']}
            for i, api in enumerate(apis)}}
        pe = FakePE({api: 100 + i * 100 for i, api in enumerate(apis)})
        accepted = []
        with patch('export_guard.shape', side_effect=lambda p, r: changes.get(apis[(r-100)//100], baseline['bindings'][apis[(r-100)//100]]['shape'])):
            result = remap(baseline, pe, accepted)
        return result, accepted

    def test_all_three_synchronized_changes_accepted(self):
        result, accepted = self.run_remap({api: self.new for api in CUSTOM_ATTRIBUTES})
        self.assertEqual(len(result), 4)
        self.assertEqual(set(accepted), CUSTOM_ATTRIBUTES)

    def test_partial_update_rejected(self):
        with self.assertRaisesRegex(ValueError, 'signatures changed'):
            self.run_remap({next(iter(CUSTOM_ATTRIBUTES)): self.new})

    def test_inconsistent_update_rejected(self):
        changes = {api: self.new for api in CUSTOM_ATTRIBUTES}
        other = self.new.copy(); other[2] = 'sub eax,6280'
        changes[next(iter(CUSTOM_ATTRIBUTES))] = other
        with self.assertRaisesRegex(ValueError, 'signatures changed'):
            self.run_remap(changes)

    def test_other_api_cannot_use_exception(self):
        with self.assertRaisesRegex(ValueError, 'signatures changed'):
            self.run_remap({'il2cpp_domain_get': self.new})

    def test_engine_metadata_only_allowed_with_identical_fingerprint(self):
        old = {'unityPlayerSha256': 'old', 'engineFingerprint': {'version': '6000.3.12f1', 'executableSections': ['code']}}
        current = dict(old, unityPlayerSha256='new')
        self.assertTrue(validate_engine(old, current))
        for fingerprint in [{'version': 'new', 'executableSections': ['code']}, {'version': '6000.3.12f1', 'executableSections': ['changed']}]:
            with self.assertRaisesRegex(ValueError, 'manual compatibility'):
                validate_engine(old, dict(current, engineFingerprint=fingerprint))
        with self.assertRaisesRegex(ValueError, 'manual compatibility'):
            validate_engine({'unityPlayerSha256': 'old'}, current)


if __name__ == '__main__':
    unittest.main()
