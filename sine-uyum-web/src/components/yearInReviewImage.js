import i18n from '../i18n';
import { shareImage, wrap } from './identityImage';
import { monthName } from './yearInReview';

// Draws the yearly recap as a shareable story image (1080x1920, 9:16).
// The favorite's poster comes from image.tmdb.org, which sends CORS headers:
// loaded with crossOrigin it keeps the canvas exportable. If it fails to load
// (or would still taint the canvas) the card is drawn without it.
const W = 1080;
const H = 1920;
const PAD = 96;
const POSTER_URL = 'https://image.tmdb.org/t/p/w342';
const POSTER_TIMEOUT_MS = 5000;

const loadPoster = (posterPath) => new Promise((resolve) => {
    if (!posterPath) return resolve(null);
    const img = new Image();
    const timer = setTimeout(() => resolve(null), POSTER_TIMEOUT_MS);
    img.crossOrigin = 'anonymous';
    img.onload = () => { clearTimeout(timer); resolve(img); };
    img.onerror = () => { clearTimeout(timer); resolve(null); };
    img.src = `${POSTER_URL}${posterPath}`;
});

// Splits text into lines that fit maxWidth; at most maxLines, the last one
// shortened with an ellipsis when the text does not fit.
const fitLines = (ctx, text, maxWidth, maxLines) => {
    const lines = [];
    let line = '';
    for (const word of text.split(' ')) {
        const test = line ? `${line} ${word}` : word;
        if (ctx.measureText(test).width > maxWidth && line) {
            lines.push(line);
            line = word;
        } else {
            line = test;
        }
    }
    if (line) lines.push(line);
    if (lines.length <= maxLines) return lines;
    const kept = lines.slice(0, maxLines);
    let last = `${kept[maxLines - 1]}…`;
    while (ctx.measureText(last).width > maxWidth && last.length > 1) last = `${last.slice(0, -2)}…`;
    kept[maxLines - 1] = last;
    return kept;
};

const drawLines = (ctx, lines, x, y, lineHeight) => {
    lines.forEach((l, i) => ctx.fillText(l, x, y + i * lineHeight));
    return y + lines.length * lineHeight;
};

const roundedClip = (ctx, x, y, w, h, r) => {
    ctx.beginPath();
    if (ctx.roundRect) ctx.roundRect(x, y, w, h, r);
    else ctx.rect(x, y, w, h);
    ctx.clip();
};

const render = (recap, poster) => {
    const t = i18n.t.bind(i18n);
    const mine = recap.isMine;
    const canvas = document.createElement('canvas');
    canvas.width = W;
    canvas.height = H;
    const ctx = canvas.getContext('2d');

    const bg = ctx.createLinearGradient(0, 0, W * 0.4, H);
    bg.addColorStop(0, '#1C1F27');
    bg.addColorStop(0.55, '#14161C');
    bg.addColorStop(1, '#0B0C10');
    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, W, H);
    const glow = ctx.createRadialGradient(W * 0.85, H * 0.12, 0, W * 0.85, H * 0.12, W * 0.8);
    glow.addColorStop(0, 'rgba(255, 112, 67, 0.35)');
    glow.addColorStop(1, 'rgba(255, 112, 67, 0)');
    ctx.fillStyle = glow;
    ctx.fillRect(0, 0, W, H);

    const label = (text, x, y) => {
        ctx.fillStyle = '#A9ADBA';
        ctx.font = '600 32px "Inter Variable", system-ui, sans-serif';
        ctx.fillText(text, x, y);
    };

    let y = 150;
    label(t('yearInReview.card.header'), PAD, y);
    y += 74;
    ctx.fillStyle = '#ffffff';
    ctx.font = '500 44px "Inter Variable", system-ui, sans-serif';
    ctx.fillText(`@${recap.userName}`, PAD, y);

    // The year, big, in a warm gradient.
    y += 210;
    ctx.font = 'bold 220px "Bricolage Grotesque Variable", system-ui, sans-serif';
    const yearFill = ctx.createLinearGradient(PAD, y - 200, PAD + 620, y);
    yearFill.addColorStop(0, '#F5B841');
    yearFill.addColorStop(1, '#FF5A5F');
    ctx.fillStyle = yearFill;
    ctx.fillText(String(recap.year), PAD - 8, y);

    // Total movies.
    y += 180;
    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 160px "Bricolage Grotesque Variable", system-ui, sans-serif';
    const total = recap.totals.movies.toLocaleString(i18n.language);
    ctx.fillText(total, PAD - 4, y);
    const totalWidth = ctx.measureText(total).width;
    ctx.fillStyle = '#F2F2F5';
    ctx.font = '500 46px "Inter Variable", system-ui, sans-serif';
    const filmsLines = fitLines(ctx, t(mine ? 'yearInReview.card.filmsMine' : 'yearInReview.card.filmsTheirs', { count: recap.totals.movies }),
        W - 2 * PAD - totalWidth - 30, 2);
    drawLines(ctx, filmsLines, PAD + totalWidth + 30, y - (filmsLines.length === 2 ? 58 : 6), 58);

    // The year's title.
    const identity = recap.identity;
    if (identity.title) {
        y += 100;
        label(t(mine ? 'yearInReview.card.titleMine' : 'yearInReview.card.titleTheirs'), PAD, y);
        y += 90;
        ctx.fillStyle = '#F5B841';
        ctx.font = 'bold 80px "Bricolage Grotesque Variable", system-ui, sans-serif';
        y = drawLines(ctx, fitLines(ctx, identity.title, W - 2 * PAD, 2), PAD, y, 90);
        if (identity.titleDescription) {
            ctx.fillStyle = '#F2F2F5';
            ctx.font = '400 38px "Inter Variable", system-ui, sans-serif';
            y = drawLines(ctx, fitLines(ctx, identity.titleDescription, W - 2 * PAD, 3), PAD, y + 4, 52);
        }
    }

    // The favorite: poster on the left when it loaded.
    const favorite = identity.favorite;
    if (favorite) {
        y += 60;
        const posterW = 200;
        const posterH = 300;
        let textX = PAD;
        if (poster) {
            ctx.save();
            roundedClip(ctx, PAD, y, posterW, posterH, 20);
            ctx.drawImage(poster, PAD, y, posterW, posterH);
            ctx.restore();
            textX = PAD + posterW + 48;
        }
        const textWidth = W - PAD - textX;
        let ty = y + 40;
        label(t(mine ? 'identity.card.favoriteMine' : 'identity.card.favoriteTheirs'), textX, ty);
        ty += 76;
        ctx.fillStyle = '#ffffff';
        ctx.font = 'bold 60px "Bricolage Grotesque Variable", system-ui, sans-serif';
        ty = drawLines(ctx, fitLines(ctx, favorite.title, textWidth, 3), textX, ty, 70);
        ctx.fillStyle = '#F5B841';
        ctx.font = '600 48px "Inter Variable", system-ui, sans-serif';
        ctx.fillText(t('yearInReview.card.rating', { rating: favorite.rating }), textX, ty + 10);
        y = Math.max(poster ? y + posterH : 0, ty + 10);
    }

    // Top genre and busiest month, side by side.
    const genre = identity.topGenres[0];
    const busiest = recap.busiestMonth;
    const columns = [
        genre && [t(mine ? 'yearInReview.card.genreMine' : 'yearInReview.card.genreTheirs'),
            t('yearInReview.card.genreValue', { name: genre.name, share: genre.share })],
        busiest && [t(mine ? 'yearInReview.card.monthMine' : 'yearInReview.card.monthTheirs'),
            t('yearInReview.card.monthValue', { month: monthName(busiest.month, i18n.language), count: busiest.count })],
    ].filter(Boolean);
    if (columns.length > 0) {
        y += 100;
        const colWidth = (W - 2 * PAD - 60) / 2;
        columns.forEach(([title, value], i) => {
            const x = PAD + i * (colWidth + 60);
            ctx.font = '600 32px "Inter Variable", system-ui, sans-serif';
            const titleLines = fitLines(ctx, title, colWidth, 2);
            ctx.fillStyle = '#A9ADBA';
            const vy = drawLines(ctx, titleLines, x, y, 42);
            ctx.fillStyle = '#ffffff';
            ctx.font = '600 46px "Inter Variable", system-ui, sans-serif';
            drawLines(ctx, fitLines(ctx, value, colWidth, 2), x, vy + 22, 56);
        });
    }

    ctx.fillStyle = 'rgba(255,255,255,0.6)';
    ctx.font = '500 32px "Inter Variable", system-ui, sans-serif';
    wrap(ctx, t('identity.card.footer'), PAD, H - 96, W - 2 * PAD, 40);
    return canvas;
};

const toBlob = (canvas) => new Promise((resolve, reject) => {
    try {
        canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('Empty image'))), 'image/png');
    } catch (e) {
        reject(e);
    }
});

export const drawYearCard = async (recap) => {
    const poster = await loadPoster(recap.identity.favorite?.posterPath);
    if (!poster) return toBlob(render(recap, null));
    try {
        return await toBlob(render(recap, poster));
    } catch {
        // A poster served without CORS headers taints the canvas: skip it.
        return toBlob(render(recap, null));
    }
};

export const shareYearCard = async (recap) => {
    const blob = await drawYearCard(recap);
    const values = { year: recap.year, userName: recap.userName, title: recap.identity.title };
    return shareImage(blob, `sineuyum-${recap.userName}-${recap.year}.png`, {
        title: i18n.t(recap.isMine ? 'yearInReview.card.shareTitleMine' : 'yearInReview.card.shareTitleTheirs', values),
        text: i18n.t(recap.isMine ? 'yearInReview.card.shareTextMine' : 'yearInReview.card.shareTextTheirs', values),
    });
};
