import { screen, waitFor } from '@testing-library/react';
import { EditNumberValue } from './EditNumberValue';
import { renderWithProviders, renderHookWithProviders } from '../../../testing/mocks';
import { useLayoutSchemaQuery } from '../../../hooks/queries/useLayoutSchemaQuery';
import { textMock } from '@studio/testing/mocks/i18nMock';
import { ComponentType } from 'app-shared/types/ComponentType';
import userEvent from '@testing-library/user-event';
import { appContextMock } from '../../../testing/appContextMock';
import { useMutation } from '@tanstack/react-query';
import { componentMocks } from '../../../testing/componentMocks';

const waitForData = async () => {
  const layoutSchemaResult = renderHookWithProviders(() => useLayoutSchemaQuery()).result;
  await waitFor(() => expect(layoutSchemaResult.current[0].isSuccess).toBe(true));
};

const renderEditNumberValue = async ({
  enumValues = null,
  maxLength = undefined,
  handleComponentChange = jest.fn(),
  componentOverrides = {},
} = {}) => {
  await waitForData();

  return renderWithProviders(
    <EditNumberValue
      handleComponentChange={handleComponentChange}
      propertyKey='maxLength'
      enumValues={enumValues}
      component={{
        id: 'c24d0812-0c34-4582-8f31-ff4ce9795e96',
        type: ComponentType.Input,
        textResourceBindings: {
          title: 'ServiceName',
        },
        maxLength,
        itemType: 'COMPONENT',
        dataModelBindings: { simpleBinding: { field: 'some-path', dataType: '' } },
        ...componentOverrides,
      }}
    />,
  );
};

describe('EditNumberValue', () => {
  afterEach(() => {
    jest.resetAllMocks();
  });

  it('should render component as input field, when not given enum prop', async () => {
    await renderEditNumberValue();

    expect(
      screen.getByRole('textbox', { name: textMock('ux_editor.component_properties.maxLength') }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
  });

  it('should render component as select, when given enum prop', async () => {
    await renderEditNumberValue({ enumValues: [1, 2, 3] });

    expect(
      screen.getByRole('combobox', { name: textMock('ux_editor.component_properties.maxLength') }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
  });

  it('should call onChange when typing in input field', async () => {
    const user = userEvent.setup();
    const mockHandleComponentChange = jest.fn((componentProperties, _) => componentProperties);
    await renderEditNumberValue({
      handleComponentChange: mockHandleComponentChange,
    });

    await user.type(screen.getByRole('textbox'), '2');

    expect(mockHandleComponentChange).toHaveReturnedWith({
      id: 'c24d0812-0c34-4582-8f31-ff4ce9795e96',
      type: ComponentType.Input,
      textResourceBindings: {
        title: 'ServiceName',
      },
      maxLength: 2,
      itemType: 'COMPONENT',
      dataModelBindings: { simpleBinding: { field: 'some-path', dataType: '' } },
    });
  });

  it('should call onChange when choosing option from select', async () => {
    const user = userEvent.setup();
    const mockHandleComponentChange = jest.fn((componentProperties, _) => componentProperties);
    await renderEditNumberValue({
      handleComponentChange: mockHandleComponentChange,
      enumValues: [1, 2, 3],
    });

    await user.selectOptions(screen.getByRole('combobox'), '1');

    expect(mockHandleComponentChange).toHaveReturnedWith({
      id: 'c24d0812-0c34-4582-8f31-ff4ce9795e96',
      type: ComponentType.Input,
      textResourceBindings: {
        title: 'ServiceName',
      },
      maxLength: 1,
      itemType: 'COMPONENT',
      dataModelBindings: { simpleBinding: { field: 'some-path', dataType: '' } },
    });
  });

  it('should save to backend and reload the preview when changing value, including the case of changing it to undefined/empty', async () => {
    const user = userEvent.setup();
    const handleSaveMutation = renderHookWithProviders(() =>
      useMutation({
        mutationFn: () => Promise.resolve(),
      }),
    ).result;
    const mockHandleComponentChange = jest
      .fn()
      .mockImplementation(async (mutationArgs, mutateOptions) => {
        await handleSaveMutation.current.mutateAsync(mutationArgs, mutateOptions);
      });

    await renderEditNumberValue({ handleComponentChange: mockHandleComponentChange });

    const input = screen.getByRole('textbox');
    await user.type(input, '12');
    // The component is updated for each keystroke, so we expect the mock to be called twice -
    // I think it should prevent this behavior with this new issue: https://github.com/Altinn/altinn-studio/issues/11989
    expect(mockHandleComponentChange).toHaveBeenCalledTimes(2);
    expect(appContextMock.updateLayoutsForPreview).toHaveBeenCalledTimes(2);
    expect(appContextMock.updateLayoutsForPreview).toHaveBeenCalledWith('test-layout-set', true);

    mockHandleComponentChange.mockClear();
    await user.clear(input);
    expect(mockHandleComponentChange).toHaveBeenCalledTimes(1);
  });

  it('should update value when propertyPath is set', async () => {
    const user = userEvent.setup();
    const mockHandleComponentChange = jest.fn((componentProperties, _) => componentProperties);
    await renderEditNumberValue({
      handleComponentChange: mockHandleComponentChange,
      componentOverrides: { propertyPath: 'definitions/inputComponent' },
    });
    await user.type(screen.getByRole('textbox'), '2');
    expect(mockHandleComponentChange).toHaveReturnedWith({
      id: 'c24d0812-0c34-4582-8f31-ff4ce9795e96',
      type: ComponentType.Input,
      textResourceBindings: {
        title: 'ServiceName',
      },
      maxLength: 2,
      itemType: 'COMPONENT',
      dataModelBindings: { simpleBinding: { field: 'some-path', dataType: '' } },
      propertyPath: 'definitions/inputComponent',
    });
  });

  describe('number of attachments', () => {
    const attachmentsErrorText = textMock('validation_errors.min_number_of_attachments_above_max');

    it('shows an error and does not save a minimum above the maximum', async () => {
      const user = userEvent.setup();
      const handleComponentChange = jest.fn();
      await renderAttachmentsNumberValue({
        propertyKey: 'minNumberOfAttachments',
        handleComponentChange,
        minNumberOfAttachments: undefined,
        maxNumberOfAttachments: 1,
      });

      await user.type(screen.getByRole('textbox'), '2');

      expect(screen.getByText(attachmentsErrorText)).toBeInTheDocument();
      expect(handleComponentChange).not.toHaveBeenCalled();
    });

    it('shows an error and does not save a maximum below the minimum', async () => {
      const user = userEvent.setup();
      const handleComponentChange = jest.fn();
      await renderAttachmentsNumberValue({
        propertyKey: 'maxNumberOfAttachments',
        handleComponentChange,
        minNumberOfAttachments: 2,
        maxNumberOfAttachments: undefined,
      });

      await user.type(screen.getByRole('textbox'), '1');

      expect(screen.getByText(attachmentsErrorText)).toBeInTheDocument();
      expect(handleComponentChange).not.toHaveBeenCalled();
    });

    it('saves a minimum equal to the maximum', async () => {
      const user = userEvent.setup();
      const handleComponentChange = jest.fn();
      await renderAttachmentsNumberValue({
        propertyKey: 'minNumberOfAttachments',
        handleComponentChange,
        minNumberOfAttachments: undefined,
        maxNumberOfAttachments: 2,
      });

      await user.type(screen.getByRole('textbox'), '2');

      expect(screen.queryByText(attachmentsErrorText)).not.toBeInTheDocument();
      expect(handleComponentChange).toHaveBeenCalledWith(
        expect.objectContaining({ minNumberOfAttachments: 2, maxNumberOfAttachments: 2 }),
        expect.anything(),
      );
    });
  });
});

type RenderAttachmentsNumberValueArgs = {
  propertyKey: 'minNumberOfAttachments' | 'maxNumberOfAttachments';
  handleComponentChange: jest.Mock;
  minNumberOfAttachments?: number;
  maxNumberOfAttachments?: number;
};

const renderAttachmentsNumberValue = async ({
  propertyKey,
  handleComponentChange,
  minNumberOfAttachments,
  maxNumberOfAttachments,
}: RenderAttachmentsNumberValueArgs) => {
  await waitForData();
  return renderWithProviders(
    <EditNumberValue
      handleComponentChange={handleComponentChange}
      propertyKey={propertyKey}
      component={{
        ...componentMocks[ComponentType.FileUpload],
        minNumberOfAttachments,
        maxNumberOfAttachments,
      }}
    />,
  );
};
