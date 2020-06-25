#!/usr/bin/env python

import sys, pickle
import xml.etree.ElementTree

# path to the Swedish Associative Thesaurus version 2 (SALDO) XML-file
saldo_path = 'saldom.xml'

if len(sys.argv) != 2:
    print('usage: {} <out>'.format(sys.argv[0]))
    quit()
outfile = sys.argv[1]

tree = xml.etree.ElementTree.parse(saldo_path)
root = tree.getroot()

wordvariants = dict()
for entry in root.iter('LexicalEntry'):
    forms = set()
    for form in entry.iter('WordForm'):
        for feat in form.iter('feat'):
            if feat.attrib['att'] == 'writtenForm':
                if feat.attrib['val'].isalpha() and len(feat.attrib['val']) > 2:
                    forms.add(feat.attrib['val'])

    # remove all variants that have already been used for other words
    overlap = set()
    for form in forms:
        if form in wordvariants:
            overlap.add(form)
    forms = forms - overlap

    for form in forms:
        wordvariants[form] = forms

nvariants = len(wordvariants)
setsize = 0
for variantset in wordvariants.values():
    setsize += len(variantset)
print(len(wordvariants), float(setsize)/nvariants)

with open(outfile, 'wb') as f:
    pickle.dump(wordvariants, f)
