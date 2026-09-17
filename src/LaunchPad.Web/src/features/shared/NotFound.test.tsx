import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { NotFound } from './NotFound';

describe('NotFound', () => {
  it('renders for an unmatched URL with a way home', () => {
    render(
      <MemoryRouter initialEntries={['/todos']}>
        <Routes>
          <Route path="/" element={<p>home</p>} />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByText('Page not found')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Go to your home page' })).toHaveAttribute('href', '/');
  });
});
