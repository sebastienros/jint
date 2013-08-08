/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-108.js
 * @description Object.defineProperty - 'name' and 'desc' are accessor properties,  name.[[Get]] is present and desc.[[Get]] is undefined (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        function getFunc() {
            return 10;
        }

        function setFunc(value) {
            obj.setVerifyHelpProp = value;
        }

        Object.defineProperty(obj, "foo", {
            get: getFunc,
            set: setFunc,
            enumerable: true,
            configurable: true
        });

        Object.defineProperty(obj, "foo", {
            set: setFunc,
            get: undefined
        });
        return accessorPropertyAttributesAreCorrect(obj, "foo", undefined, setFunc, "setVerifyHelpProp", true, true);
    }
runTestCase(testcase);
