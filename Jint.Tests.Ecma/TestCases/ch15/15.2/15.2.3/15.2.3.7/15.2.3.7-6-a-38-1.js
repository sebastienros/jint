/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-38-1.js
 * @description Object.defineProperties - 'P' exists in 'O' is an accessor property, test 'P' makes no change if 'desc' is generic descriptor without any attribute (8.12.9 step 5)
 */


function testcase() {

        var obj = {};
        var getFunc = function () {
            return 12;
        };
        Object.defineProperties(obj, {
            foo: {
                get: getFunc,
                enumerable: true,
                configurable: true
            }
        });

        Object.defineProperties(obj, { foo: {} });

        return accessorPropertyAttributesAreCorrect(obj, "foo", getFunc, undefined, undefined, true, true);
    }
runTestCase(testcase);
