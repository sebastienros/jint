/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-62.js
 * @description Object.defineProperties - desc.enumerable and P.enumerable are two boolean values with different values (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { 
            value: 10, 
            enumerable: false, 
            configurable: true 
        });

        Object.defineProperties(obj, {
            foo: {
                enumerable: true
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", 10, false, true, true);
    }
runTestCase(testcase);
