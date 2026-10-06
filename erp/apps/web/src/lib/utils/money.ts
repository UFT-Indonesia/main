const idr = new Intl.NumberFormat('id-ID', { style: 'currency', currency: 'IDR', maximumFractionDigits: 0 });

/** Rupiah, no decimals: "Rp 225.000". */
export const formatIdr = (amount: number): string => idr.format(amount);
