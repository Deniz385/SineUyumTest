import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { Autocomplete, TextField, CircularProgress, Box } from '@mui/material';
import { useAuth } from '../context/AuthContext';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';

// onMovieSelect prop'u eklendi.
export const MovieSearchBar = ({ onMovieSelect }) => {
  const [open, setOpen] = useState(false);
  const [options, setOptions] = useState([]);
  const [loading, setLoading] = useState(false);
  const [inputValue, setInputValue] = useState('');
  const navigate = useNavigate();
  const { token } = useAuth();

  useEffect(() => {
    const delayDebounceFn = setTimeout(() => {
      if (inputValue) {
        setLoading(true);
        axios.get(`${API_URL}/api/movies/search`, {
          headers: { 'Authorization': `Bearer ${token}` },
          params: { query: inputValue }
        }).then(response => {
          const movies = response.data.results || [];
          const uniqueMovies = Array.from(new Map(movies.map(movie => [movie.id, movie])).values());
          setOptions(uniqueMovies);
          setLoading(false);
        }).catch(err => {
          console.error("Film arama hatası:", err);
          setLoading(false);
        });
      } else {
        setOptions([]);
      }
    }, 500);

    return () => clearTimeout(delayDebounceFn);
  }, [inputValue, token]);

  const handleOnChange = (event, value) => {
    if (value && value.id) {
      // Eğer onMovieSelect prop'u verildiyse onu çağır
      if (onMovieSelect) {
        onMovieSelect(value);
      } else {
        // Yoksa eskisi gibi sayfaya yönlendir
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
      sx={{ width: '100%', marginRight: 2 }} // Genişliği tam yapalım
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
            // Navbar'da değilse rengi normal yap
            style: onMovieSelect ? {} : { color: 'white' }, 
            endAdornment: (
              <>
                {loading ? <CircularProgress color="inherit" size={20} /> : null}
                {params.InputProps.endAdornment}
              </>
            ),
          }}
          sx={onMovieSelect ? {} : { // Navbar'da değilse border'ı normal yap
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