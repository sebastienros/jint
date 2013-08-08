/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-110.js
 * @description Object.defineProperty - 'name' and 'desc' are accessor properties, both desc.[[Set]] and name.[[Set]] are two different values (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        function setFunc1() {
            return 10;
        }

        Object.defineProperty(obj, "foo", {
            set: setFunc1,
            enumerable: true,
            configurable: true
        });

        function setFunc2(value) {
            obj.setVerifyHelpProp = value;
        }

        Object.defineProperty(obj, "foo", {
            set: setFunc2
        });
        return accessorPropertyAttributesAreCorrect(obj, "foo", undefined, setFunc2, "setVerifyHelpProp", true, true);
    }
runTestCase(testcase);
