/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-96.js
 * @description Object.defineProperties - 'P' is data property, properties.value is present and P.value is undefined (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", {
            value: undefined,
            enumerable: true,
            writable: true,
            configurable: true 
        });

        Object.defineProperties(obj, {
            foo: {
                value: 200
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", 200, true, true, true);
    }
runTestCase(testcase);
