import { renderWithProviders } from '../../../testing/mocks';
import { ConfigNumberProperties, type ConfigNumberPropertiesProps } from './ConfigNumberProperties';
import { componentMocks } from '../../../testing/componentMocks';
import InputSchema from '../../../testing/schemas/json/component/Input.schema.v1.json';
import FileUploadSchema from '../../../testing/schemas/json/component/FileUpload.schema.v1.json';
import userEvent from '@testing-library/user-event';
import { screen } from '@testing-library/react';
import { textMock } from '@studio/testing/mocks/i18nMock';
import { ComponentType } from 'app-shared/types/ComponentType';
import {
  cancelConfigAndVerify,
  getPropertyByRole,
  openConfigAndVerify,
  saveConfigChanges,
} from './testConfigUtils';

const defaultProperty = 'someNumberProperty';

describe('ConfigNumberProperties', () => {
  it('should render property text for "preselectedOptionIndex" with button suffix', () => {
    renderConfigNumberProperties({
      numberPropertyKeys: ['preselectedOptionIndex'],
    });
    expect(getPropertyByRole('button', 'preselectedOptionIndex_button')).toBeInTheDocument();
  });

  it('should render EditNumberValue components when keepEditOpen is true', () => {
    renderConfigNumberProperties({
      keepEditOpen: true,
    });
    expect(getPropertyByRole('combobox', defaultProperty)).toBeInTheDocument();
  });

  it('should be able to toggle number property config', async () => {
    const user = userEvent.setup();
    renderConfigNumberProperties();
    await openConfigAndVerify({ user, property: defaultProperty });
    await cancelConfigAndVerify(user);
  });

  it('should call handleComponentUpdate when saving a new value', async () => {
    const user = userEvent.setup();
    const handleComponentUpdate = jest.fn();
    renderConfigNumberProperties({
      handleComponentUpdate,
      schema: {
        ...InputSchema,
        properties: {
          someNumberProperty: {
            type: 'number',
          },
        },
      },
    });

    await openConfigAndVerify({ user, property: defaultProperty });
    const textBox = getPropertyByRole('textbox', defaultProperty);
    await user.type(textBox, '2');
    await saveConfigChanges(user);

    expect(handleComponentUpdate).toHaveBeenCalledWith({
      ...componentMocks.Input,
      someNumberProperty: 2,
    });
  });

  it('should disable save when the pending minimum number of attachments ends up above a newly saved maximum', async () => {
    const user = userEvent.setup();
    const fileUploadComponent = {
      ...componentMocks[ComponentType.FileUpload],
      minNumberOfAttachments: 1,
      maxNumberOfAttachments: 3,
    };
    const props: Partial<ConfigNumberPropertiesProps> = {
      numberPropertyKeys: ['minNumberOfAttachments'],
      schema: FileUploadSchema,
    };
    const { rerender } = renderConfigNumberProperties({ ...props, component: fileUploadComponent });

    await openConfigAndVerify({ user, property: 'minNumberOfAttachments' });
    const textBox = getPropertyByRole('textbox', 'minNumberOfAttachments');
    await user.clear(textBox);
    await user.type(textBox, '3');
    const saveButton = screen.getByRole('button', { name: textMock('general.save') });
    expect(saveButton).toBeEnabled();

    rerender(
      <ConfigNumberProperties
        {...defaultProps}
        {...props}
        component={{ ...fileUploadComponent, maxNumberOfAttachments: 1 }}
      />,
    );

    expect(saveButton).toBeDisabled();
    expect(
      screen.getByText(textMock('validation_errors.min_number_of_attachments_above_max')),
    ).toBeInTheDocument();
  });

  const defaultProps: ConfigNumberPropertiesProps = {
    numberPropertyKeys: [defaultProperty],
    schema: {
      ...InputSchema,
      properties: {
        someNumberProperty: {
          type: 'number',
          enum: [1, 2, 3],
        },
      },
    },
    component: componentMocks.Input,
    handleComponentUpdate: jest.fn(),
  };
  const renderConfigNumberProperties = (props: Partial<ConfigNumberPropertiesProps> = {}) => {
    return renderWithProviders(<ConfigNumberProperties {...defaultProps} {...props} />, {});
  };
});
