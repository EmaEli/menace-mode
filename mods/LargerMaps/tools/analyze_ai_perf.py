#!/usr/bin/env python3
"""Summarize bounded Larger Maps 1.4.10 AI PERF records from Latest.log.

Usage: python tools/analyze_ai_perf.py path/to/Latest.log
This is a non-invasive log parser and does not run MENACE or load DLLs.
"""
import argparse
import re
from pathlib import Path

TURN_RE = re.compile(r"AI PERF turn=#(?P<id>\d+) round=(?P<round>-?\d+) faction=(?P<faction>0x[0-9a-fA-F]+) wall=(?P<wall>\d+(?:\.\d+)?)ms pending=(?P<pending>\d+) droppedTasks=(?P<dropped>\d+)")
OPS_RE = re.compile(r"AI PERF ops turn=#(?P<id>\d+)")
UNIT_RE = re.compile(r"AI PERF unit turn=#(?P<id>\d+) actor=(?P<actor>0x[0-9a-fA-F]+) template=(?P<template>\S+)")
BEHAVIOR_RE = re.compile(r"AI PERF behavior turn=#(?P<id>\d+) actor=(?P<actor>0x[0-9a-fA-F]+) template=(?P<template>\S+) id=(?P<behavior>\S+)")
METRIC_RE = re.compile(r"(?P<key>[A-Za-z][\w-]*)=n=(?P<n>\d+),sum=(?P<sum>\d+(?:\.\d+)?)ms,max=(?P<max>\d+(?:\.\d+)?)ms")
ACTOR_RE = re.compile(r"scope=actor-activation actor=(?P<actor>0x[0-9a-fA-F]+) template=(?P<template>[^ ]+) closedBy=(?P<boundary>[^ ]+)")


def parse_lines(lines):
    turns = {}
    for line in lines:
        if 'AI PERF' not in line:
            continue
        summary = TURN_RE.search(line)
        if summary:
            info = summary.groupdict()
            entry = turns.setdefault(int(info['id']), {'units': [], 'ops': {}, 'metrics': {}})
            actor = ACTOR_RE.search(line)
            entry.update(round=int(info['round']), faction=info['faction'], wall_ms=float(info['wall']),
                         actor=(actor['actor'] if actor else None),
                         template=(actor['template'] if actor else None),
                         closed_by=(actor['boundary'] if actor else None),
                         pending=int(info['pending']), dropped=int(info['dropped']),
                         metrics={m['key']: {'calls': int(m['n']), 'sum_ms': float(m['sum']),
                                             'max_ms': float(m['max'])} for m in
                                  (r.groupdict() for r in METRIC_RE.finditer(line))})
            continue
        ops = OPS_RE.search(line)
        if ops:
            entry = turns.setdefault(int(ops['id']), {'units': [], 'ops': {}, 'metrics': {}})
            entry['ops'] = {m['key']: {'calls': int(m['n']), 'sum_ms': float(m['sum']),
                                      'max_ms': float(m['max'])} for m in
                            (r.groupdict() for r in METRIC_RE.finditer(line))}
            continue
        behavior = BEHAVIOR_RE.search(line)
        if behavior:
            info = behavior.groupdict()
            entry = turns.setdefault(int(info['id']), {'units': [], 'ops': {}, 'metrics': {}})
            metric = next((m.groupdict() for m in METRIC_RE.finditer(line) if m.group('key') == 'collect'), None)
            if metric:
                entry.setdefault('behaviors', []).append({
                    'actor': info['actor'], 'template': info['template'], 'id': info['behavior'],
                    'calls': int(metric['n']), 'sum_ms': float(metric['sum']), 'max_ms': float(metric['max'])})
            continue
        unit = UNIT_RE.search(line)
        if unit:
            info = unit.groupdict()
            entry = turns.setdefault(int(info['id']), {'units': [], 'ops': {}, 'metrics': {}})
            entry['units'].append({'actor': info['actor'], 'template': info['template'],
                                   'metrics': {m['key']: {'calls': int(m['n']), 'sum_ms': float(m['sum']),
                                                          'max_ms': float(m['max'])} for m in
                                               (r.groupdict() for r in METRIC_RE.finditer(line))}})
    return turns


def report(turns):
    result = ['MENACE Larger Maps 1.4.10 AI performance (wall-clock observation)',
              'AI PERF turn=# is one ACTOR ACTIVATION (not an entire faction round).',
              'Do not add nested/concurrent sums: durations can overlap. Task polling adds up to ~0.5s lag.']
    completed = [(i, x) for i, x in turns.items() if 'wall_ms' in x]
    if not completed:
        return '\n'.join(result + ['No complete AI PERF turn summaries found. Check diagnostics, hooks and game log.'])
    for id_, turn in sorted(completed, key=lambda item: item[1]['wall_ms'], reverse=True):
        label = (f" actor={turn['template']} ({turn['actor']}) closedBy={turn['closed_by']}"
                 if turn.get('actor') else '')
        result.append(f"AI activation #{id_} round={turn['round']} faction={turn['faction']} wall={turn['wall_ms']/1000:.2f}s{label} pending={turn['pending']} dropped={turn['dropped']}")
        for category, metrics in (('phases', turn.get('metrics', {})), ('operations', turn.get('ops', {}))):
            items = [(k, m) for k, m in metrics.items() if m['calls']]
            items.sort(key=lambda item: item[1]['sum_ms'], reverse=True)
            if items:
                result.append('  ' + category + ': ' + '; '.join(
                    f"{k}: n={m['calls']} sum={m['sum_ms']:.1f}ms max={m['max_ms']:.1f}ms"
                    for k, m in items))
        for behavior in turn.get('behaviors', []):
            result.append(f"  behavior={behavior['id']} actor={behavior['actor']} "
                          f"template={behavior['template']} n={behavior['calls']} "
                          f"sum={behavior['sum_ms']:.1f}ms max={behavior['max_ms']:.1f}ms")
        for unit in turn.get('units', []):
            hottest = sorted((x for x in unit['metrics'].items() if x[1]['calls']),
                             key=lambda pair: pair[1]['sum_ms'], reverse=True)[:3]
            result.append(f"  actor={unit['actor']} template={unit['template']} " +
                          ', '.join(f"{name} sum={m['sum_ms']:.1f}ms" for name, m in hottest))
    return '\n'.join(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('log', type=Path, help='MENACE Latest.log with optional AI PERF entries')
    args = parser.parse_args()
    with args.log.open('r', encoding='utf-8', errors='replace') as file:
        print(report(parse_lines(file)))


if __name__ == '__main__':
    main()
