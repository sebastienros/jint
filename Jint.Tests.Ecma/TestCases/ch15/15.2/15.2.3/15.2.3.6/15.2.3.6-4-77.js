/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-77.js
 * @description Object.defineProperty - both desc.[[Set]] and name.[[Set]] are two objects which refer to the same object (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        function setFunc(value) {
            obj.setVerifyHelpProp = value;
        }

        Object.defineProperty(obj, "foo", { set: setFunc });

        Object.defineProperty(obj, "foo", { set: setFunc });
        return accessorPropertyAttributesAreCorrect(obj, "foo", undefined, setFunc, "setVerifyHelpProp", false, false);
    }
runTestCase(testcase);
