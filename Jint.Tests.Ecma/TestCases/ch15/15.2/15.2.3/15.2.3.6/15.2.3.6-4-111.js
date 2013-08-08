/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-111.js
 * @description Object.defineProperty  - 'name' and 'desc' are accessor properties, name.[[Set]] is present and desc.[[Set]] is undefined (8.12.9 step 12)
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
            set: undefined,
            get: getFunc
        });


        var desc = Object.getOwnPropertyDescriptor(obj, "foo");
        return obj.hasOwnProperty("foo") && typeof (desc.set) === "undefined";
    }
runTestCase(testcase);
