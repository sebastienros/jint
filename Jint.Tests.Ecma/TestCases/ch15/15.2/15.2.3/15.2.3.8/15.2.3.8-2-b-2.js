/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-b-2.js
 * @description Object.seal - the [[Configurable]] attribute of own accessor property of 'O' is set from true to false and other attributes of the property are unaltered
 */


function testcase() {
        var obj = {};
        obj.variableForHelpVerify = "data";

        function setFunc(value) {
            obj.variableForHelpVerify = value;
        }
        function getFunc() {
            return 10;
        }
        Object.defineProperty(obj, "foo", {
            get: getFunc,
            set: setFunc,
            enumerable: true,
            configurable: true
        });
        var preCheck = Object.isExtensible(obj);
        Object.seal(obj);

        return preCheck && accessorPropertyAttributesAreCorrect(obj, "foo", getFunc, setFunc, "variableForHelpVerify", true, false);
    }
runTestCase(testcase);
