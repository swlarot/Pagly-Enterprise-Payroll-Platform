const formatter = new Intl.NumberFormat('es-PA', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export function formatCurrency(amount: number | null | undefined): string {
  return 'B/. ' + formatter.format(amount || 0);
}

export const formatBalboas = formatCurrency;

export function formatCurrencyShort(amount: number | null | undefined): string {
  const value = amount || 0;
  if (value >= 1_000_000) return `B/.${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 1000) return `B/.${(value / 1000).toFixed(1)}k`;
  return formatCurrency(value);
}
