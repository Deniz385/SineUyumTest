// Shared navigation data: the top bar's sections and relative times.
import DynamicFeedRoundedIcon from '@mui/icons-material/DynamicFeedRounded';
import ExploreRoundedIcon from '@mui/icons-material/ExploreRounded';
import EventRoundedIcon from '@mui/icons-material/EventRounded';
import ChatBubbleRoundedIcon from '@mui/icons-material/ChatBubbleRounded';

const TIME_UNITS = [
  ['year', 31536000],
  ['month', 2592000],
  ['day', 86400],
  ['hour', 3600],
  ['minute', 60],
];

// Shows a time as "5 minutes ago" in the active language.
export const timeSince = (date, language, justNow) => {
  const seconds = Math.floor((new Date() - new Date(date)) / 1000);
  const format = new Intl.RelativeTimeFormat(language, { numeric: 'always' });
  for (const [unit, unitSeconds] of TIME_UNITS) {
    const interval = seconds / unitSeconds;
    if (interval > 1) return format.format(-Math.floor(interval), unit);
  }
  return justNow;
};

// Main sections of the top bar (design system §7). Between 900 and 1024px
// they are icons with tooltips; wider, icons with labels. Below 900px the
// bottom navigation takes over.
export const NAV_LINKS = [
  { to: '/home', labelKey: 'nav.feed', Icon: DynamicFeedRoundedIcon, match: ['/feed', '/home'] },
  { to: '/discover', labelKey: 'nav.discover', Icon: ExploreRoundedIcon, match: ['/discover', '/lists', '/search'] },
  { to: '/my-event', labelKey: 'nav.event', Icon: EventRoundedIcon, match: ['/my-event', '/subscription'] },
  { to: '/messages', labelKey: 'nav.messages', Icon: ChatBubbleRoundedIcon, match: ['/messages'] },
];

export const isNavActive = (link, pathname) => link.match.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));

