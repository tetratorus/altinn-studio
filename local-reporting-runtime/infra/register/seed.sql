-- Synthetic Register fixtures for the local reporting runtime. All identifiers are
-- Tenor-style synthetic values (birth month +80); nothing here refers to real people or companies.
--
--   party 50100001  person  02856221086  "AUTORISERT TESTPERSON"    user 1001  -> daglig-leder for org
--   party 50100002  person  15877749964  "UAUTORISERT TESTPERSON"   user 1002  -> no roles
--   party 50100003  org     310548510    "LOKAL RAPPORTERINGSVIRKSOMHET AS"
--   party 50100004  org     991825827    "TESTDEPARTEMENTET" (service owner "ttd" behind the app's machine token)
--
-- Idempotent: safe to re-run.
BEGIN;

INSERT INTO register.party(uuid, id, party_type, display_name, person_identifier, organization_identifier, created, updated, is_deleted)
VALUES
  ('11111111-0000-4000-8000-000000000001'::uuid, 50100001, 'person'::register.party_type,       'AUTORISERT TESTPERSON',           '02856221086', NULL,        now(), now(), false),
  ('11111111-0000-4000-8000-000000000002'::uuid, 50100002, 'person'::register.party_type,       'UAUTORISERT TESTPERSON',          '15877749964', NULL,        now(), now(), false),
  ('22222222-0000-4000-8000-000000000001'::uuid, 50100003, 'organization'::register.party_type, 'LOKAL RAPPORTERINGSVIRKSOMHET AS', NULL,          '310548510', now(), now(), false),
  ('22222222-0000-4000-8000-000000000002'::uuid, 50100004, 'organization'::register.party_type, 'TESTDEPARTEMENTET',                NULL,          '991825827', now(), now(), false)
ON CONFLICT (uuid) DO NOTHING;

UPDATE register.party SET ext_urn = CASE party_type
    WHEN 'person' THEN 'urn:altinn:person:identifier-no:' || person_identifier
    ELSE 'urn:altinn:organization:identifier-no:' || organization_identifier END
WHERE ext_urn IS NULL AND id IN (50100001, 50100002, 50100003, 50100004);

INSERT INTO register.person(uuid, first_name, middle_name, last_name, short_name, date_of_birth, date_of_death, address, mailing_address, source)
VALUES
  ('11111111-0000-4000-8000-000000000001'::uuid, 'AUTORISERT',   NULL, 'TESTPERSON', 'TESTPERSON AUTORISERT',   '1962-05-02', NULL, '(,,,,,0150,OSLO)', '(Testveien 1,0150,OSLO)', 'npr'::register.person_source),
  ('11111111-0000-4000-8000-000000000002'::uuid, 'UAUTORISERT', NULL, 'TESTPERSON', 'TESTPERSON UAUTORISERT', '1977-07-15', NULL, '(,,,,,5003,BERGEN)', '(Testveien 2,5003,BERGEN)', 'npr'::register.person_source)
ON CONFLICT (uuid) DO NOTHING;

INSERT INTO register.organization(uuid, unit_status, unit_type, telephone_number, mobile_number, fax_number, email_address, internet_address, mailing_address, business_address, source)
VALUES
  ('22222222-0000-4000-8000-000000000001'::uuid, 'N', 'AS', NULL, NULL, NULL, 'post@lokal-rapportering.example', NULL, '(Rapportveien 3,0150,OSLO)', '(Rapportveien 3,0150,OSLO)', 'ccr'::register.organization_source),
  ('22222222-0000-4000-8000-000000000002'::uuid, 'N', 'ORGL', NULL, NULL, NULL, 'post@testdepartementet.example', NULL, '(Statsveien 1,0030,OSLO)', '(Statsveien 1,0030,OSLO)', 'ccr'::register.organization_source)
ON CONFLICT (uuid) DO NOTHING;

INSERT INTO register.party_source_ref(party_uuid, "source", source_identifier, source_created, source_updated)
VALUES
  ('22222222-0000-4000-8000-000000000001'::uuid, 'ccr'::register.party_source, '310548510', now(), now()),
  ('22222222-0000-4000-8000-000000000002'::uuid, 'ccr'::register.party_source, '991825827', now(), now()),
  ('11111111-0000-4000-8000-000000000001'::uuid, 'npr'::register.party_source, '02856221086', now(), now()),
  ('11111111-0000-4000-8000-000000000002'::uuid, 'npr'::register.party_source, '15877749964', now(), now())
ON CONFLICT DO NOTHING;

-- Authorized person is "daglig leder" (DAGL) of the reporting org; the unauthorized one holds nothing.
INSERT INTO register.external_role_assignment("source", identifier, from_party, to_party)
VALUES
  ('ccr'::register.external_role_source, 'daglig-leder'::register.identifier,
   '22222222-0000-4000-8000-000000000001'::uuid, '11111111-0000-4000-8000-000000000001'::uuid)
ON CONFLICT DO NOTHING;

INSERT INTO register."user"(uuid, user_id, is_active)
VALUES
  ('11111111-0000-4000-8000-000000000001'::uuid, 1001, true),
  ('11111111-0000-4000-8000-000000000002'::uuid, 1002, true)
ON CONFLICT DO NOTHING;

COMMIT;
