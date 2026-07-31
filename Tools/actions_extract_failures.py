#!/usr/bin/env python
"""
This script ingests XML files in an nunit format and extracts failed tests.
This script is intended to be part of a CI/CD pipeline to detect failures.
"""

import os
import json
import xmltodict

def testcase_proc(json_input):
    """Recursively process a test-case to extract all failures.

    This function is a helper that expects to be fed with extracted test-case elements.
    """
    if isinstance(json_input, dict):
        for key, value in json_input.items():
            if key == '@result' and value == 'Failed':
                print(json_input)
                yield json_input
    elif isinstance(json_input, list):
        for item in json_input:
            yield from testcase_proc(item)

def item_generator(json_input):
    """Recursively process the test report extract all failures."""
    if isinstance(json_input, dict):
        for key, value in json_input.items():
            if key == 'test-case':
                yield from testcase_proc(value)
            else:
                yield from item_generator(value)
    elif isinstance(json_input, list):
        for item in json_input:
            yield from item_generator(item)

def extract(filename):
    """Extract all failures from an XML file."""
    with open(filename, 'r', encoding='utf-8') as xml_file:
        xml_data = xmltodict.parse(xml_file.read())

    failures = []
    for item in item_generator(xml_data['test-run']):
        failures.append(item)

    return failures

all_fails = []
all_fails.extend(extract('./test_results/logs/Content.Tests.xml'))
all_fails.extend(extract('./test_results/logs/Content.IntegrationTests.xml'))

# HONK START - upstream runs this on their own repo, where every failure is theirs
# to action. The fork runs the same suite, so it also re-reports upstream
# heisentests we can't fix here (NukeOpsTest and ExpireIdCardTest are both tracked
# upstream and have been flaky there for over a year). Report only fork tests.
#
# This filters what gets *reported*, deliberately not what gets *run*: the full
# suite still executes, so the PoolManager churn that surfaces order-dependent
# fork flakes is preserved. Narrowing the test run instead would shrink that
# churn and could hide exactly the kind of bug this is meant to catch.
#
# Fork tests all carry "RussStation" in their fully qualified name
# (Content.IntegrationTests.Tests.RussStation.*, Content.Tests.*.RussStation.*).
all_fails = [fail for fail in all_fails if 'RussStation' in fail['@fullname']]
# HONK END

# Create the list of processed failures to create a matrix for later jobs.
matrix = []
for fail in all_fails:
    matrix.append(
        {
            'name': fail['@name'],
            'fullname': fail['@fullname'],
            'failure': fail['failure']['message'],
            'output': fail['output']
        }
    )

# Clean up the output to ensure that no '@' symbols escape.
json_data = json.dumps(matrix).replace('@', '')

# Write to the action step output
with open(os.environ.get('GITHUB_OUTPUT'), 'a') as f:
    f.write(f'matrix={json_data}\ncount={len(matrix)}\n')
