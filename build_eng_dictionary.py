#!/usr/bin/env python

import sys
import os
import string
import nltk

# path to the directory containing MRSTY.RRF and MRCONSO.RRF from UMLS
UMLS_path = 'UMLS'

# which UMLS semantic groups to include
target_tuis = {
    'T047', # Disease or Syndrome
    'T184', # Sign or Symptom
    'T046', # Pathologic Function
    'T200', # Clinical Drug
    'T121', # Pharmacologic Substance
    'T195', # Antibiotic
}

# which source vocabularies to use
source_priorities = {
    ('SNOMEDCT_US', 'PT'): '1',
    ('SNOMEDCT_US', 'SY'): '1'
}

trans = str.maketrans('', '', string.punctuation)

def generate_variants(word, word_dict):
    # it is possible to add word variant code here
    return {word}

if len(sys.argv) != 2:
    print('usage: {} <out>'.format(sys.argv[0]))
    quit()
outfile = sys.argv[1]

matching_cuis = set()
with open(os.path.join(UMLS_path, 'MRSTY.RRF'), 'r') as f:
    for line in f:
        cui, tui = line.split('|')[0:2]
        if tui in target_tuis:
            matching_cuis.add(cui)

with open(outfile, 'w') as outf:
    with open(os.path.join(UMLS_path, 'MRCONSO.RRF'), 'r', encoding='utf-8') as inf:
        for line in inf:
            fields = line.split('|')
            cui = fields[0]
            source = (fields[11], fields[12])
            term = fields[14]

            if cui in matching_cuis and source in source_priorities:
                priority = source_priorities[source]
                tokens = [token.lower().translate(trans)
                            for token in nltk.word_tokenize(term)]
                tokens = [generate_variants(token)
                            for token in tokens if token != '']
                termstr = ' '.join([','.join(token) for token in tokens])
                outf.write(f'{cui} {priority} {termstr}\n')
