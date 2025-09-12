import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../api/axiosConfig'; // DÜZELTME: axios yerine merkezi api import ediliyor
import { Autocomplete, TextField, CircularProgress, Box } from '@mui/material';
import { useAuth } from '../context/AuthContext';

export const MovieSearchBar = ({ onMovieSelect }) => {
  const [open, setOpen] = useState(false);
  const [options, setOptions] = useState([]);
  const [loading, setLoading] = useState(false);
  const [inputValue, setInputValue] = useState('');
  const navigate = useNavigate();
  const { user } = useAuth(); // DÜZELTME: token yerine user kullanılıyor

  useEffect(() => {
    // Eğer kullanıcı giriş yapmamışsa arama yapma
    if (!user) {
        setOptions([]);
        return;
    }

    const delayDebounceFn = setTimeout(() => {
      if (inputValue) {
        setLoading(true);
        // DÜZELTME: 'api' kullanılıyor ve header kaldırılıyor. Interceptor token'ı ekleyecek.
        api.get(`/api/movies/search`, {
          params: { query: inputValue }
        }).then(response => {
          const movies = response.data.results || [];
          const uniqueMovies = Array.from(new Map(movies.map(movie => [movie.id, movie])).values());
          setOptions(uniqueMovies);
        }).catch(err => {
          console.error("Film arama hatası:", err);
        }).finally(() => {
            setLoading(false);
        });
      } else {
        setOptions([]);
      }
    }, 500);

    return () => clearTimeout(delayDebounceFn);
  }, [inputValue, user]); // DÜZELTME: Bağımlılık 'user' olarak değiştirildi

  const handleOnChange = (event, value) => {
    if (value && value.id) {
      if (onMovieSelect) {
        onMovieSelect(value);
      } else {
        navigate(`/movie/${value.id}`);
      }
      setOpen(false);
      setOptions([]);
      setInputValue('');
    }
  };

  return (
    <Autocomplete
      id="movie-search-autocomplete"
      sx={{ width: '100%', marginRight: 2 }}
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
      loading={loading}
      freeSolo
      disableClearable
      renderInput={(params) => (
        <TextField
          {...params}
          placeholder="Film ara..."
          variant="outlined"
          size="small"
          InputProps={{
            ...params.InputProps,
            style: onMovieSelect ? {} : { color: 'white' }, 
            endAdornment: (
              <>
                {loading ? <CircularProgress color="inherit" size={20} /> : null}
                {params.InputProps.endAdornment}
              </>
            ),
          }}
          sx={onMovieSelect ? {} : {
            '& .MuiOutlinedInput-root': {
              '& fieldset': { borderColor: '#6c757d' },
              '&:hover fieldset': { borderColor: 'white' },
            },
          }}
        />
      )}
      renderOption={(props, option) => (
        <Box component="li" sx={{ '& > img': { mr: 2, flexShrink: 0 } }} {...props} key={option.id}>
          <img
            loading="lazy"
            width="40"
            src={option.poster_path ? `https://image.tmdb.org/t/p/w200${option.poster_path}`: '/vite.svg'}
            alt=""
          />
          {option.title} ({option.release_date?.split('-')[0]})
        </Box>
      )}
    />
  );
};