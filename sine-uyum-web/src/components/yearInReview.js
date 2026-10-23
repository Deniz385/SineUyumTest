// Shared helpers for the yearly recap ("year in review").

export const yearInReviewPath = (userId, year) => `/profile/${userId}/year${year ? `/${year}` : ''}`;

// The recap worth opening on a given day: until mid-January the year that
// just ended, otherwise the current year so far.
export const defaultRecapYear = (now = new Date()) =>
    now.getMonth() === 0 && now.getDate() <= 15 ? now.getFullYear() - 1 : now.getFullYear();

// The feed announces the recap in December and in the first half of January
// (then the previous year's); null the rest of the year.
export const bannerRecapYear = (now = new Date()) => {
    if (now.getMonth() === 11) return now.getFullYear();
    if (now.getMonth() === 0 && now.getDate() <= 15) return now.getFullYear() - 1;
    return null;
};

// A dismissed banner stays hidden for that year. Storage can throw (private
// mode, blocked site data); then the banner simply shows again next visit.
const dismissKey = (year) => `yearInReviewBannerDismissed:${year}`;

export const isBannerDismissed = (year) => {
    try {
        return localStorage.getItem(dismissKey(year)) === '1';
    } catch {
        return false;
    }
};

export const dismissBanner = (year) => {
    try {
        localStorage.setItem(dismissKey(year), '1');
    } catch {
        // Ignore: hidden for this visit only.
    }
};

// ISO weekday (1 = Monday ... 7 = Sunday) as a name in the given language.
// 2024-01-01 was a Monday.
export const weekdayName = (isoWeekday, language) =>
    new Intl.DateTimeFormat(language, { weekday: 'long' }).format(new Date(2024, 0, isoWeekday));

export const monthName = (month, language) =>
    new Intl.DateTimeFormat(language, { month: 'long' }).format(new Date(2024, month - 1, 1));
