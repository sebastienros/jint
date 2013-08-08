/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-31.js
 * @description Object.defineProperties - 'desc' is data descriptor, test setting all attribute values of 'P' (8.12.9 step 4.a.i)
 */


function testcase() {
        var obj = {};

        Object.defineProperties(obj, {
            prop: {
                value: 1002,
                writable: false,
                enumerable: false,
                configurable: false
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "prop", 1002, false, false, false);

    }
runTestCase(testcase);
