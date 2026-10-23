import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api from '../api/axiosConfig';
import { Autocomplete, TextField, CircularProgress, Box, InputAdornment, Typography } from '@mui/material';
import SearchRoundedIcon from '@mui/icons-material/SearchRounded';
import { useAuth } from '../context/AuthContext';
import { PosterImage } from './ui/PosterImage';

// Movie search with suggestions. Without `onMovieSelect` choosing a film
// opens its page (top bar); `onNavigate` runs after that (closes the phone
// search sheet). `autoFocus` focuses the box on mount.
export const MovieSearchBar = ({ onMovieSelect, onNavigate, autoFocus = false, size = 'small' }) => {
  const [open, setOpen] = useState(false);
  const [options, setOptions] = useState([]);
  const [loading, setLoading] = useState(false);
  const [inputValue, setInputValue] = useState('');
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { user } = useAuth();

  useEffect(() => {
    // Don't search when nobody is logged in
    if (!user) {
        setOptions([]);
        return;
    }

    const delayDebounceFn = setTimeout(() => {
      if (inputValue) {
        setLoading(true);
        // The interceptor adds the token and language headers.
        api.get(`/api/v1/movies/search`, {
          params: { query: inputValue }
        }).then(response => {
          const movies = response.data.results || [];
          const uniqueMovies = Array.from(new Map(movies.map(movie => [movie.id, movie])).values());
          setOptions(uniqueMovies);
        }).catch(err => {
          console.error("Movie search failed:", err);
        }).finally(() => {
            setLoading(false);
        });
      } else {
        setOptions([]);
      }
    }, 500);

    return () => clearTimeout(delayDebounceFn);
  }, [inputValue, user]);

  const handleOnChange = (event, value) => {
    if (value && value.id) {
      if (onMovieSelect) {
        onMovieSelect(value);
      } else {
        navigate(`/movie/${value.id}`);
        onNavigate?.();
      }
      setOpen(false);
      setOptions([]);
      setInputValue('');
    }
  };

  return (
    <Autocomplete
      id="movie-search-autocomplete"
      sx={{ width: '100%' }}
      open={open}
      onOpen={() => setOpen(true)}
      onClose={() => setOpen(false)}
      onChange={handleOnChange}
      onInputChange={(event, newInputValue) => {
        setInputValue(newInputValue);
      }}
      isOptionEqualToValue={(option, value) => option.id === value.id}
      getOptionLabel={(option) => option.title || ""}
      options={options}
      // The server already searched (original titles too); don't filter again.
      filterOptions={(x) => x}
      loading={loading}
      loadingText={t('common.loadingEllipsis')}
      noOptionsText={inputValue ? t('search.noMovies') : t('search.typeToSearch')}
      freeSolo
      disableClearable
      slotProps={{ paper: { sx: { mt: 0.5 } } }}
      renderInput={(params) => (
        <TextField
          {...params}
          autoFocus={autoFocus}
          placeholder={t('search.moviesPlaceholder')}
          inputProps={{ ...params.inputProps, 'aria-label': t('search.moviesPlaceholder'), enterKeyHint: 'search' }}
          variant="outlined"
          size={size}
          InputProps={{
            ...params.InputProps,
            startAdornment: onMovieSelect ? params.InputProps.startAdornment : (
              <InputAdornment position="start">
                <SearchRoundedIcon fontSize="small" sx={{ color: 'text.secondary' }} />
              </InputAdornment>
            ),
            endAdornment: (
              <>
                {loading ? <CircularProgress color="inherit" size={18} /> : null}
                {params.InputProps.endAdornment}
              </>
            ),
          }}
          sx={onMovieSelect ? {} : { '& .MuiOutlinedInput-root': { borderRadius: 999, pl: 1.5 } }}
        />
      )}
      renderOption={(props, option) => {
        const { key, ...optionProps } = props;
        return (
          <Box component="li" key={option.id ?? key} {...optionProps} sx={{ gap: 1.5 }}>
            <Box sx={{ width: 32, flexShrink: 0 }}>
              <PosterImage path={option.poster_path} title={option.title} width={32} radius="sm" decorative />
            </Box>
            <Box sx={{ minWidth: 0 }}>
              <Typography variant="body1" noWrap sx={{ fontWeight: 600 }}>{option.title}</Typography>
              {option.release_date && (
                <Typography variant="body2" color="text.secondary" className="tabular-nums">{option.release_date.split('-')[0]}</Typography>
              )}
            </Box>
          </Box>
        );
      }}
    />
  );
};
