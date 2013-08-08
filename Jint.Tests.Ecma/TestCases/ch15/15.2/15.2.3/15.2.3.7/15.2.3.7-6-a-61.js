/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-61.js
 * @description Object.defineProperties - both desc.enumerable and P.enumerable are boolean values with the same value (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { 
            value: 10, 
            enumerable: false 
        });

        Object.defineProperties(obj, {
            foo: {
                enumerable: false
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", 10, false, false, false);
    }
runTestCase(testcase);
