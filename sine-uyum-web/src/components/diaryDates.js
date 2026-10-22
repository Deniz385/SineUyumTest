// Diary dates are calendar days ("yyyy-MM-dd") with no time zone. They are
// parsed as local dates: new Date("2026-10-05") would be UTC midnight and show
// as the previous day west of UTC.

export const EARLIEST_WATCHED_ON = '1900-01-01';

const pad = (n) => String(n).padStart(2, '0');

export const toIsoDay = (date) => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;

export const todayIsoDay = () => toIsoDay(new Date());

export const parseIsoDay = (iso) => {
    const [year, month, day] = iso.split('-').map(Number);
    return new Date(year, month - 1, day);
};

// Month names in the given language, January first; style is an
// Intl month style ('short': "Jan", "Oca"...; 'narrow': "J", "O"...).
export const monthNames = (language, style) => {
    const format = new Intl.DateTimeFormat(language, { month: style });
    return Array.from({ length: 12 }, (_, i) => format.format(new Date(2000, i, 1)));
};
