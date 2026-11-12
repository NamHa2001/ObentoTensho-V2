import { Schemas } from '@ot/api';

type Group = Schemas['PublicOptionGroupDto'];
export type Selection = Record<number, number[]>;

// BR-MNU-18..20: quy tắc chọn tùy chọn; server kiểm lại khi đặt đơn
export function isOrderable(option: Schemas['PublicOptionDto']): boolean {
  return option.status === 'available';
}

export function defaultSelection(groups: Group[]): Selection {
  const selection: Selection = {};
  for (const group of groups) {
    selection[group.id] = group.options.filter((o) => o.isDefault && isOrderable(o)).slice(0, group.maxSelect).map((o) => o.id);
  }

  return selection;
}

export function toggle(selection: Selection, group: Group, optionId: number): Selection {
  const current = selection[group.id] ?? [];
  if (group.selectionType === 'one') {
    return { ...selection, [group.id]: [optionId] };
  }

  if (current.includes(optionId)) {
    return { ...selection, [group.id]: current.filter((id) => id !== optionId) };
  }

  return current.length >= group.maxSelect ? selection : { ...selection, [group.id]: [...current, optionId] };
}

export function requiredCount(group: Group): number {
  return group.isRequired ? Math.max(1, group.minSelect) : group.minSelect;
}

export function isComplete(selection: Selection, groups: Group[]): boolean {
  return groups.every((g) => (selection[g.id]?.length ?? 0) >= requiredCount(g));
}

export function unitPrice(basePrice: number, selection: Selection, groups: Group[]): number {
  const chosen = new Set(Object.values(selection).flat());
  return groups.flatMap((g) => g.options).reduce((sum, o) => (chosen.has(o.id) ? sum + o.priceDelta : sum), basePrice);
}
