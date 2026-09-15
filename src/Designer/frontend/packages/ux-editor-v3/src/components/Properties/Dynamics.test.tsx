import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FormItemContext } from '../../containers/FormItemContext';
import { renderWithMockStore } from '../../testing/mocks';
import { formItemContextProviderMock } from '../../testing/formItemContextMocks';
import { Dynamics } from './Dynamics';
import { textMock } from '@studio/testing/mocks/i18nMock';
import type { FormComponent } from '../../types/FormComponent';
import { createQueryClientMock } from 'app-shared/mocks/queryClientMock';
import { QueryKey } from 'app-shared/types/QueryKey';
import { org, app } from '@studio/testing/testids';
import { layoutSet1NameMock } from '../../testing/layoutSetsMock';

const user = userEvent.setup();

// Test data:
const conditionalRenderingTestId = 'conditional-rendering';
const expressionsTestId = 'expressions';

// Mocks:
jest.mock('./ConditionalRendering', () => ({
  ConditionalRendering: () => <div data-testid={conditionalRenderingTestId} />,
}));
jest.mock('../config/Expressions', () => ({
  Expressions: () => <div data-testid={expressionsTestId} />,
}));

describe('Dynamics', () => {
  afterEach(jest.clearAllMocks);

  it('should render new expressions editor by default', async () => {
    await render();
    expect(screen.getByTestId(expressionsTestId)).toBeInTheDocument();
    expect(screen.queryByTestId(conditionalRenderingTestId)).not.toBeInTheDocument();
  });

  it('should not render switch if ruleHandler is not found', async () => {
    await render();
    const oldDynamicsSwitch = screen.queryByRole('switch', {
      name: textMock('right_menu.show_old_dynamics'),
    });
    expect(oldDynamicsSwitch).not.toBeInTheDocument();
  });

  it('should render default unchecked switch if ruleHandler is found', async () => {
    await render({}, true);
    const oldDynamicsSwitch = screen.getByRole('switch', {
      name: textMock('right_menu.show_old_dynamics'),
    });
    expect(oldDynamicsSwitch).toBeInTheDocument();
    expect(oldDynamicsSwitch).not.toBeChecked();
  });

  it('should render old dynamics when enabling switch if ruleHandler is found', async () => {
    await render({}, true);
    const oldDynamicsSwitch = screen.getByRole('switch', {
      name: textMock('right_menu.show_old_dynamics'),
    });
    await user.click(oldDynamicsSwitch);
    expect(screen.queryByTestId(expressionsTestId)).not.toBeInTheDocument();
    expect(screen.getByTestId(conditionalRenderingTestId)).toBeInTheDocument();
  });

  it('should render unknown component alert when component is unknown for Studio', async () => {
    const formType = 'randomUnknownComponent' as unknown as FormComponent;
    await render({ formItem: { ...formItemContextProviderMock.formItem, type: formType } });
    expect(
      screen.getByText(
        textMock('ux_editor.edit_component.unknown_component', {
          componentName: formType,
        }),
      ),
    );
  });
});

const render = async (
  props: Partial<FormItemContext> = {},
  conditionalRulesExist = false,
) => {
  const queryClient = createQueryClientMock();
  queryClient.setQueryData(
    [QueryKey.RuleHandler, org, app, layoutSet1NameMock],
    conditionalRulesExist ? [{ name: 'x', type: 'condition', inputs: {} }] : [],
  );

  return renderWithMockStore({}, {}, queryClient)(
    <FormItemContext.Provider
      value={{
        ...formItemContextProviderMock,
        ...props,
      }}
    >
      <Dynamics />
    </FormItemContext.Provider>,
  );
};
