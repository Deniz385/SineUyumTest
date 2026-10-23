import i18n from '../i18n';

// Draws the film identity card as a shareable PNG (1080x1350, Instagram
// ratio). Posters are not used: images from another domain "taint" the
// canvas and block exporting it.
// Texts follow the active UI language; the identity's title, description and
// genre names already arrive localized from the API.
const W = 1080;
const H = 1350;

// Draws text word-wrapped to maxWidth; returns the y of the line after it.
export const wrap = (ctx, text, x, y, maxWidth, lineHeight) => {
    const words = text.split(' ');
    let line = '';
    for (const word of words) {
        const test = line ? `${line} ${word}` : word;
        if (ctx.measureText(test).width > maxWidth && line) {
            ctx.fillText(line, x, y);
            line = word;
            y += lineHeight;
        } else {
            line = test;
        }
    }
    if (line) ctx.fillText(line, x, y);
    return y + lineHeight;
};

const fmt = (n) => Number(n).toLocaleString(i18n.language, { minimumFractionDigits: 1, maximumFractionDigits: 1 });

// isMine: your own card (second person, like Spotify Wrapped) or someone else's (third person).
export const drawIdentityCard = (identity, isMine = true) => {
    const t = i18n.t.bind(i18n);
    const canvas = document.createElement('canvas');
    canvas.width = W;
    canvas.height = H;
    const ctx = canvas.getContext('2d');

    const bg = ctx.createLinearGradient(0, 0, W, H);
    bg.addColorStop(0, '#1b1038');
    bg.addColorStop(1, '#0d2a4a');
    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, W, H);

    const pad = 90;
    let y = 150;
    ctx.fillStyle = '#9fb3ff';
    ctx.font = '600 34px system-ui, sans-serif';
    ctx.fillText(t('identity.card.header'), pad, y);

    y += 80;
    ctx.fillStyle = '#ffffff';
    ctx.font = '500 44px system-ui, sans-serif';
    ctx.fillText(`@${identity.userName}`, pad, y);

    y += 120;
    ctx.fillStyle = '#ffd54f';
    ctx.font = 'bold 92px system-ui, sans-serif';
    y = wrap(ctx, identity.title, pad, y, W - 2 * pad, 100);

    ctx.fillStyle = '#e3e8ff';
    ctx.font = '400 40px system-ui, sans-serif';
    y = wrap(ctx, identity.titleDescription, pad, y + 10, W - 2 * pad, 54);

    y += 40;
    ctx.fillStyle = '#ffffff';
    ctx.font = '600 46px system-ui, sans-serif';
    ctx.fillText(t('identity.card.summary', { count: identity.totalRatings, average: fmt(identity.averageRating) }), pad, y);

    if (identity.topGenres.length > 0) {
        y += 90;
        ctx.fillStyle = '#9fb3ff';
        ctx.font = '600 34px system-ui, sans-serif';
        ctx.fillText(isMine ? t('identity.card.topGenresMine') : t('identity.card.topGenresTheirs'), pad, y);
        for (const genre of identity.topGenres) {
            y += 70;
            const barWidth = Math.max(40, (W - 2 * pad - 260) * genre.share / 100);
            ctx.fillStyle = 'rgba(255,255,255,0.12)';
            ctx.fillRect(pad + 260, y - 34, W - 2 * pad - 260, 40);
            ctx.fillStyle = '#7c9cff';
            ctx.fillRect(pad + 260, y - 34, barWidth, 40);
            ctx.fillStyle = '#ffffff';
            ctx.font = '500 38px system-ui, sans-serif';
            ctx.fillText(genre.name, pad, y);
            ctx.font = '600 30px system-ui, sans-serif';
            ctx.fillText(t('identity.card.genreShare', { share: genre.share }), pad + 270 + barWidth, y - 4);
        }
    }

    const line = (label, text) => {
        y += 90;
        ctx.fillStyle = '#9fb3ff';
        ctx.font = '600 34px system-ui, sans-serif';
        ctx.fillText(label, pad, y);
        y += 58;
        ctx.fillStyle = '#ffffff';
        ctx.font = '500 40px system-ui, sans-serif';
        y = wrap(ctx, text, pad, y, W - 2 * pad, 52) - 52;
    };
    if (identity.contrarianTake) {
        const take = identity.contrarianTake;
        const values = { title: take.title, average: fmt(take.tmdbAverage), rating: take.rating };
        line(
            isMine ? t('identity.card.contrarianMine') : t('identity.card.contrarianTheirs'),
            isMine ? t('identity.contrarianLineMine', values) : t('identity.contrarianLineTheirs', values)
        );
    }
    if (identity.favorite) {
        line(isMine ? t('identity.card.favoriteMine') : t('identity.card.favoriteTheirs'), `${identity.favorite.title} · ${identity.favorite.rating}/10`);
    }

    ctx.fillStyle = 'rgba(255,255,255,0.6)';
    ctx.font = '500 32px system-ui, sans-serif';
    ctx.fillText(t('identity.card.footer'), pad, H - 90);

    return new Promise((resolve) => canvas.toBlob(resolve, 'image/png'));
};

// Uses the system share sheet when the device supports it (mobile browsers:
// Instagram, WhatsApp...), otherwise downloads the file.
export const shareImage = async (blob, fileName, { title, text }) => {
    const file = new File([blob], fileName, { type: 'image/png' });
    if (navigator.canShare?.({ files: [file] })) {
        await navigator.share({ files: [file], title, text });
        return 'shared';
    }
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = file.name;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    return 'downloaded';
};

export const shareIdentityImage = async (identity, isMine = true) => {
    const blob = await drawIdentityCard(identity, isMine);
    return shareImage(blob, `sineuyum-${identity.userName}.png`, {
        title: i18n.t('identity.card.shareTitle'),
        text: i18n.t('identity.card.shareText', { title: identity.title }),
    });
};
