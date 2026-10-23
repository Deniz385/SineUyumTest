// The server answers errors as a problem object ({ title }) or a plain string.
export const apiErrorMessage = (err, fallback) => {
    const data = err?.response?.data;
    if (typeof data === 'string' && data.trim()) return data;
    return data?.title || data?.message || fallback;
};
