namespace Jint.Tests.Browser.Files;

using Browser = global::Jint.Browser.Browser;

/// <summary>HTML's drag data store, FileList, and selected-file state.</summary>
public sealed class FileTransferTests
{
    [Test]
    public async Task FileItemsProjectIntoALiveFileListAndCanBeAssignedToAFileInput()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='upload' type='file' multiple><input id='text'>");

        await page.EvaluateAsync(
            """
            (async () => {
              const first = new File(
                [new Uint8Array([104, 101, 108, 108, 111])],
                'greeting.txt',
                { type: 'text/plain', lastModified: 123 });
              const second = new File(['{"ok":true}'], 'data.json', { type: 'application/json', lastModified: 456 });
              const transfer = new DataTransfer();
              const firstItem = transfer.items.add(first);
              transfer.items.add(second);
              const input = document.getElementById('upload');
              input.files = transfer.files;

              const beforeRemoval = [
                transfer.items instanceof DataTransferItemList,
                transfer.files instanceof FileList,
                input.files === transfer.files,
                input.files.length,
                input.files[0] === first,
                input.files.item(1) === second,
                input.files.item(2) === null,
                [...input.files].map(file => file.name).join(','),
                FileList.prototype[Symbol.iterator] === Array.prototype[Symbol.iterator],
                firstItem.kind,
                firstItem.type,
                firstItem.getAsFile() === first,
                first.name,
                first.type,
                first.lastModified,
                await first.text(),
                await second.text()
              ].join('|');

              transfer.items.remove(0);
              const afterRemoval = [
                transfer.items.length,
                transfer.files.length,
                transfer.files[0].name,
                input.files.length
              ].join(',');
              transfer.items.clear();
              const afterClear = [transfer.items.length, transfer.files.length, input.files.length].join(',');
              window.transferResult = [beforeRemoval, afterRemoval, afterClear].join(';');
            })()
            """);
        (await page.WaitForAsync("window.transferResult !== undefined", TimeSpan.FromSeconds(30))).Should().BeTrue();
        var result = await page.EvaluateAsync<string>("window.transferResult");

        result.Should().Be(
            "true|true|true|2|true|true|true|greeting.txt,data.json|true|file|text/plain|true|"
            + "greeting.txt|text/plain|123|hello|{\"ok\":true};1,1,data.json,1;0,0,0");
    }

    [Test]
    public async Task StringItemsFollowTheDragDataStoreListRules()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>files</p>");

        await page.EvaluateAsync(
            """
            (async () => {
              const transfer = new DataTransfer();
              const item = transfer.items.add('hello', 'TEXT/PLAIN');
              transfer.setData('url', 'https://example.test/');
              let duplicate;
              try {
                transfer.items.add('other', 'TEXT/PLAIN');
                duplicate = 'no throw';
              } catch (error) {
                duplicate = error.name;
              }
              const callbackData = await new Promise(resolve => item.getAsString(resolve));
              const noMatch = action => {
                try { action(); return 'no throw'; } catch (error) { return error.name; }
              };
              const literalType = new DataTransfer().items.add('literal', 'TEXT').type;
              const replacement = new DataTransfer();
              replacement.setData('custom', 'one');
              const replaced = replacement.items[0];
              replacement.setData('other', 'middle');
              replacement.setData('custom', 'two');
              const clearUndefined = new DataTransfer();
              clearUndefined.setData('text', 'gone');
              clearUndefined.clearData(undefined);
              const uri = new DataTransfer();
              uri.setData(' URL ', '# comment\r\nhttps://first.test/\nhttps://second.test/');
              const fileItem = uri.items.add(new File(['x'], 'x.txt'));
              const disabledTransfer = new DataTransfer();
              const removed = disabledTransfer.items.add('x', 'custom');
              disabledTransfer.items.remove(0);
              const sameTypes = transfer.types === transfer.types;
              const overloads = [
                noMatch(() => new DataTransfer().items.add()),
                noMatch(() => new DataTransfer().items.add('one argument')),
                new DataTransfer().items.add(new File(['x'], 'x.txt'), 'text/custom').kind,
                noMatch(() => transfer.files.item()),
                noMatch(() => new DataTransfer().items.remove()),
                noMatch(() => transfer.getData()),
                noMatch(() => transfer.setData()),
                noMatch(() => item.getAsString()),
                noMatch(() => fileItem.getAsString(1)),
                noMatch(() => removed.getAsString(1)),
                noMatch(() => item.getAsString(null))
              ].join(',');
              const beforeClear = [
                transfer.items.length,
                transfer.items[0] === item,
                item.kind,
                item.type,
                item.getAsFile() === null,
                callbackData,
                transfer.getData('TEXT'),
                transfer.getData('Url'),
                transfer.types.join(','),
                Object.isFrozen(transfer.types),
                sameTypes,
                duplicate,
                literalType,
                replacement.items[0] !== replaced,
                replaced.kind === '',
                replacement.types.join(','),
                replacement.getData('custom'),
                clearUndefined.items.length,
                uri.getData(' url '),
                overloads
              ].join('|');

              transfer.clearData('text');
              const afterClearData = [
                transfer.items.length,
                transfer.types.join(','),
                transfer.getData('text'),
                item.kind,
                item.type
              ].join('|');
              transfer.items.remove(0);
              window.transferResult = [beforeClear, afterClearData, transfer.items.length].join(';');
            })()
            """);
        (await page.WaitForAsync("window.transferResult !== undefined", TimeSpan.FromSeconds(30))).Should().BeTrue();
        var result = await page.EvaluateAsync<string>("window.transferResult");

        result.Should().Be(
            "2|true|string|text/plain|true|hello|hello|https://example.test/|"
            + "text/plain,text/uri-list|true|true|NotSupportedError|text|true|true|other,custom|two|0|https://first.test/|"
            + "TypeError,TypeError,string,TypeError,TypeError,TypeError,TypeError,TypeError,TypeError,TypeError,no throw;"
            + "1|text/uri-list|||;0");
    }

    [Test]
    public async Task SelectedFilesParticipateInInputValueValidityAndReset()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            "<form id='form'><input id='upload' type='file' required><input id='text'></form>");

        var result = await page.EvaluateAsync<string>(
            """
            (() => {
              const input = document.getElementById('upload');
              const select = name => {
                const transfer = new DataTransfer();
                transfer.items.add(new File(['content'], name, { type: 'text/plain' }));
                input.files = transfer.files;
                return transfer.files;
              };

              const first = select('first.txt');
              let nonEmpty;
              try { input.value = 'not allowed'; nonEmpty = 'no throw'; } catch (error) { nonEmpty = error.name; }
              const selected = [input.value, input.validity.valueMissing, input.checkValidity(), nonEmpty].join('|');

              input.value = '';
              const cleared = [
                input.files !== first,
                input.files.length,
                first.length,
                input.validity.valueMissing
              ].join('|');

              const second = select('second.txt');
              document.getElementById('form').reset();
              const reset = [
                input.files !== second,
                input.files.length,
                second.length,
                input.validity.valueMissing
              ].join('|');

              const third = select('third.txt');
              input.type = 'text';
              const textState = [input.files === null, input.value].join('|');
              input.type = 'file';
              const fileState = [
                input.files !== third,
                input.files.length,
                third.length,
                input.validity.valueMissing
              ].join('|');

              const fourth = select('fourth.txt');
              input.setAttribute('type', 'text');
              input.setAttribute('type', 'file');
              const attributeState = [
                input.files !== fourth,
                input.files.length,
                fourth.length,
                input.validity.valueMissing
              ].join('|');

              const fifth = select('fifth.txt');
              input.files = undefined;
              const undefinedFiles = [input.files === fifth, input.files.length].join('|');
              input.value = null;
              const nullValue = [input.files !== fifth, input.files.length, fifth.length].join('|');

              const transition = action => {
                const source = select('source.txt');
                action();
                const away = input.files === null;
                input.type = 'file';
                return [away, input.files.length, source.length].join('|');
              };
              const mutationStates = [
                transition(() => input.setAttributeNS(null, 'type', 'text')),
                transition(() => input.removeAttributeNS(null, 'type')),
                transition(() => {
                  input.remove();
                  input.getAttributeNode('type').value = 'text';
                }),
                transition(() => {
                  const attribute = document.createAttribute('type');
                  attribute.value = 'text';
                  input.attributes.setNamedItem(attribute);
                }),
                transition(() => input.attributes.removeNamedItem('type')),
                transition(() => input.toggleAttribute('type'))
              ].join(',');

              const cloneSource = select('clone.txt');
              const clone = input.cloneNode();
              const imported = document.importNode(input);
              const copiedStates = [
                clone.value,
                clone.files.length,
                clone.validity.valueMissing,
                clone.checkValidity(),
                imported.value,
                imported.files.length,
                imported.validity.valueMissing,
                imported.checkValidity(),
                cloneSource.length
              ].join('|');

              return [
                selected,
                cleared,
                reset,
                textState,
                fileState,
                attributeState,
                undefinedFiles,
                nullValue,
                mutationStates,
                copiedStates
              ].join(';');
            })()
            """);

        result.Should().Be(
            @"C:\fakepath\first.txt|false|true|InvalidStateError;"
            + "true|0|1|true;true|0|1|true;true|;true|0|1|true;true|0|1|true;"
            + "true|1;true|0|1;"
            + "true|0|1,true|0|1,true|0|1,true|0|1,true|0|1,true|0|1;"
            + "|0|true|false||0|true|false|1");
    }

    [Test]
    public async Task TransferInterfacesAndInputFilesHaveTheirWebIdlShape()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='upload' type='file'><input id='text'>");

        var result = await page.EvaluateAsync<string>(
            """
            (() => {
              const transfer = new DataTransfer();
              let illegalConstructor;
              let badAssignment;
              try { new FileList(); } catch (error) { illegalConstructor = error.message; }
              try { document.getElementById('upload').files = []; } catch (error) { badAssignment = error.name; }
              const descriptor = Object.getOwnPropertyDescriptor(globalThis, 'DataTransfer');
              return [
                transfer instanceof DataTransfer,
                Object.prototype.toString.call(transfer),
                Object.prototype.toString.call(transfer.items),
                Object.prototype.toString.call(transfer.files),
                DataTransfer.length,
                illegalConstructor,
                badAssignment,
                document.getElementById('text').files === null,
                descriptor.writable,
                descriptor.enumerable,
                descriptor.configurable
              ].join('|');
            })()
            """);

        result.Should().Be(
            "true|[object DataTransfer]|[object DataTransferItemList]|[object FileList]|0|Illegal constructor|"
            + "TypeError|true|true|false|true");
    }
}
