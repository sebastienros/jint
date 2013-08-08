/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-113.js
 * @description Object.defineProperty - 'name' and 'desc' are accessor properties, name.enumerable and desc.enumerable are different values (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        function getFunc() {
            return 10;
        }

        Object.defineProperty(obj, "foo", {
            get: getFunc,
            enumerable: true,
            configurable: true
        });

        Object.defineProperty(obj, "foo", {
            get: getFunc,
            enumerable: false
        });

        return accessorPropertyAttributesAreCorrect(obj, "foo", getFunc, undefined, undefined, false, true);
    }
runTestCase(testcase);
