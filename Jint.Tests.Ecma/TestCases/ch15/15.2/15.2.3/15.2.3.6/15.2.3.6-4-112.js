/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-112.js
 * @description Object.defineProperty - 'name' and 'desc' are accessor properties, name.[[Set]] is undefined and desc.[[Set]] is function (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        function getFunc() {
            return 10;
        }

        Object.defineProperty(obj, "foo", {
            set: undefined,
            get: getFunc,
            enumerable: true,
            configurable: true
        });

        function setFunc(value) {
            obj.setVerifyHelpProp = value;
        }

        Object.defineProperty(obj, "foo", {
            set: setFunc
        });
        return accessorPropertyAttributesAreCorrect(obj, "foo", getFunc, setFunc, "setVerifyHelpProp", true, true);
    }
runTestCase(testcase);
