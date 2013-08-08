/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-75.js
 * @description Object.defineProperty - both desc.[[Get]] and name.[[Get]] are two objects which refer to the same object (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        function getFunc() {
            return 10;
        }
        function setFunc(value) {
            obj.helpVerifySet = value;
        }

        Object.defineProperty(obj, "foo", {
            get: getFunc,
            set: setFunc
        });

        Object.defineProperty(obj, "foo", { get: getFunc });
        return accessorPropertyAttributesAreCorrect(obj, "foo", getFunc, setFunc, "helpVerifySet", false, false);
    }
runTestCase(testcase);
