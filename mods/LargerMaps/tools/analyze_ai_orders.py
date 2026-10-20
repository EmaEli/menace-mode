#!/usr/bin/env python3
"""Read-only AI ROUTE contact/order timeline; observations, not a causal test."""
import argparse
import re
from pathlib import Path

EVENT = re.compile(r"AI ROUTE ([A-Z-]+) map=(\S+) round=(-?\d+) ")
FIELD = re.compile(r"(?:^| )([A-Za-z][A-Za-z0-9-]*)=([^\s;\[\]]+)")
CONTACT = re.compile(r"[0-9A-Fa-f]+:([^ ;\]]+) known=(True|False) ttl=(-?\d+)")


def parse_lines(lines, faction):
    """Key by map/round. Never equate a CHOICE poll with a committed Move."""
    rounds = {}
    limits = 0
    for number, line in enumerate(lines, 1):
        if 'AI ROUTE LIMIT ' in line or 'AI ROUTE BUFFER LIMIT ' in line:
            limits += 1
        event = EVENT.search(line)
        if not event:
            continue
        kind, map_id, round_id = event.groups()
        fields = dict(FIELD.findall(line[event.end():]))
        if fields.get('faction') != str(faction):
            continue
        key = (map_id, int(round_id))
        row = rounds.setdefault(key, {
            'contacts': None, 'contacts_complete': False, 'start_known': set(),
            'first_known_line': None, 'positive': [set(), set()], 'moves': [0, 0],
            'warnings': [],
        })
        if kind == 'ROUND':
            if row['contacts'] is not None:
                row['warnings'].append('Repeated round summary; possible session/pointer reuse')
            contacts = CONTACT.findall(line[event.end():])
            row['contacts'] = [(name, int(ttl)) for name, known, ttl in contacts
                               if known == 'True']
            row['contacts_complete'] = (
                fields.get('opponents') == fields.get('shown') == str(len(contacts)))
            if any((known == 'True') != (int(ttl) >= 0) for _, known, ttl in contacts):
                row['warnings'].append('Contact known/TTL mismatch')
        elif kind == 'STATE':
            count = fields.get('knownOpponents')
            if count is not None and count.isdigit():
                if fields.get('phase') == 'round-start':
                    row['start_known'].add(int(count))
                if int(count) > 0 and row['first_known_line'] is None:
                    row['first_known_line'] = number
        elif kind == 'MOVE-EVAL':
            score = fields.get('score', '')
            if re.fullmatch(r'-?\d+', score) and int(score) > 0 and 'id' in fields:
                row['positive'][row['first_known_line'] is not None].add(fields['id'])
        elif kind == 'MOVE':
            row['moves'][row['first_known_line'] is not None] += 1
    return rounds, limits


def report(rounds, limits):
    result = [
        'AI order/contact observations (not proof of causality).',
        'Before/after = first STATE with knownOpponents>0 in that map/round.',
        'This is an observed boundary, not an exact sighting/evaluation timestamp.',
        'Positive = unique actors; Move = OnExecute callbacks, not completed arrivals.',
        f'Route limit warnings in input (all factions): {limits}. Missing events are not zero activity.',
    ]
    if not rounds:
        return '\n'.join(result + ['No matching faction records.'])
    for (map_id, round_id), row in sorted(rounds.items()):
        contacts = row['contacts']
        if contacts is None:
            contact_text = 'missing round snapshot'
        else:
            names = ', '.join(f'{name}:TTL={ttl}' for name, ttl in contacts) or 'none'
            coverage = 'complete' if row['contacts_complete'] else 'INCOMPLETE'
            contact_text = f'{names} ({coverage})'
        counts = ','.join(map(str, sorted(row['start_known']))) or 'missing'
        boundary = row['first_known_line']
        positive = [len(actors) for actors in row['positive']]
        result.append(f'map={map_id} round={round_id} startKnown={counts} '
                      f'contacts=[{contact_text}] firstKnownLine={boundary} '
                      f'positiveBefore/After={positive[0]}/{positive[1]} '
                      f'moveBefore/After={row["moves"][0]}/{row["moves"][1]}')
        result.extend('  WARNING: ' + warning for warning in row['warnings'])
    return '\n'.join(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('log', type=Path)
    parser.add_argument('--faction', type=int, required=True, help='Native faction index, not a pointer')
    args = parser.parse_args()
    with args.log.open(encoding='utf-8', errors='replace') as source:
        print(report(*parse_lines(source, args.faction)))


if __name__ == '__main__':
    main()