import { act, render, renderHook, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ServicesContextProvider } from 'app-shared/contexts/ServicesContext';
import { queriesMock } from 'app-shared/mocks/queriesMock';
import { createQueryClientMock } from 'app-shared/mocks/queryClientMock';
import { FeatureFlagsContextProvider } from '@studio/feature-flags';
import { typedLocalStorage } from '@studio/pure-functions';
import { ColorScheme } from '../../enums/ColorScheme';
import { COLOR_SCHEME_STORAGE_KEY } from '../../utils/colorSchemeUtils';
import type { HeaderContextProps } from './HeaderContext';
import { HeaderContextProvider, useHeaderContext } from './HeaderContext';
import { renderWithProviders } from '../../testing/mocks';
import { textMock } from '@studio/testing/mocks/i18nMock';

const SettingsLinkConsumer = () => {
  const { profileMenuGroups } = useHeaderContext();
  const allItems = profileMenuGroups?.flatMap((group) => group.items) ?? [];
  const settingsItem = allItems.find((item) => item.itemName === textMock('settings'));
  const href = settingsItem?.action.type === 'link' ? settingsItem.action.href : null;
  return <div data-testid='settings-href'>{href ?? 'none'}</div>;
};

const renderHeaderContext = (options?: Parameters<typeof renderWithProviders>[1]) =>
  renderWithProviders(
    <HeaderContextProvider>
      <SettingsLinkConsumer />
    </HeaderContextProvider>,
    options,
  );

describe('HeaderContext', () => {
  it('should render children', () => {
    renderWithProviders(
      <HeaderContextProvider>
        <button>My button</button>
      </HeaderContextProvider>,
    );

    expect(screen.getByRole('button', { name: 'My button' })).toBeInTheDocument();
  });

  it('should provide a useHeaderContext hook', () => {
    const TestComponent = () => {
      const {} = useHeaderContext();
      return <div data-testid='context'></div>;
    };

    renderWithProviders(
      <HeaderContextProvider>
        <TestComponent />
      </HeaderContextProvider>,
    );

    expect(screen.getByTestId('context')).toHaveTextContent('');
  });

  it('should throw an error when useHeaderContext is used outside of a HeaderContextProvider', () => {
    const TestComponent = () => {
      useHeaderContext();
      return <div data-testid='context'>Test</div>;
    };

    expect(() => render(<TestComponent />)).toThrow(
      'useHeaderContext must be used within a HeaderContextProvider',
    );
  });

  it('should include user settings link in profile menu', () => {
    renderHeaderContext();

    expect(screen.getByTestId('settings-href')).not.toHaveTextContent(/^none$/);
  });

  describe('color scheme', () => {
    afterEach(() => {
      typedLocalStorage.removeItem(COLOR_SCHEME_STORAGE_KEY);
      document.documentElement.removeAttribute('data-color-scheme');
    });

    it('should provide the light color scheme by default', () => {
      const { result } = renderHeaderContextHook();
      expect(result.current.colorScheme).toBe(ColorScheme.Light);
    });

    it('should include a color scheme group with one item per color scheme', () => {
      const { result } = renderHeaderContextHook();
      const group = getColorSchemeMenuGroup(result.current);
      expect(group.showName).toBe(true);
      expect(group.items.map((item) => item.itemName)).toEqual([
        textMock('dashboard.color_scheme_light'),
        textMock('dashboard.color_scheme_dark'),
        textMock('dashboard.color_scheme_auto'),
      ]);
    });

    it('should mark the current color scheme as active', () => {
      const { result } = renderHeaderContextHook();
      const activeItems = getColorSchemeMenuGroup(result.current).items.filter(
        (item) => item.isActive,
      );
      expect(activeItems.map((item) => item.itemName)).toEqual([
        textMock('dashboard.color_scheme_light'),
      ]);
    });

    it('should change, store and apply the color scheme when a color scheme item is clicked', () => {
      const { result } = renderHeaderContextHook();
      const darkItem = getColorSchemeMenuGroup(result.current).items.find(
        (item) => item.itemName === textMock('dashboard.color_scheme_dark'),
      );

      act(() => {
        if (darkItem.action.type === 'button') darkItem.action.onClick();
      });

      expect(result.current.colorScheme).toBe(ColorScheme.Dark);
      expect(typedLocalStorage.getItem(COLOR_SCHEME_STORAGE_KEY)).toBe(ColorScheme.Dark);
      expect(document.documentElement).toHaveAttribute('data-color-scheme', ColorScheme.Dark);
    });
  });
});

function renderHeaderContextHook() {
  const queryClient = createQueryClientMock();
  return renderHook(() => useHeaderContext(), {
    wrapper: ({ children }) => (
      <MemoryRouter>
        <ServicesContextProvider {...queriesMock} client={queryClient}>
          <FeatureFlagsContextProvider value={{ flags: [] }}>
            <HeaderContextProvider>{children}</HeaderContextProvider>
          </FeatureFlagsContextProvider>
        </ServicesContextProvider>
      </MemoryRouter>
    ),
  });
}

function getColorSchemeMenuGroup(context: Partial<HeaderContextProps>) {
  return context.profileMenuGroups.find(
    (group) => group.name === textMock('dashboard.color_scheme'),
  );
}
