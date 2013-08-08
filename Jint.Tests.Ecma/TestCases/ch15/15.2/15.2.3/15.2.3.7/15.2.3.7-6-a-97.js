/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-97.js
 * @description Object.defineProperties - 'P' is data property, P.writable and properties.writable are different values (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { 
            value: 100, 
            enumerable: true, 
            writable: false, 
            configurable: true 
        });

        Object.defineProperties(obj, {
            foo: {
                writable: true
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", 100, true, true, true);
    }
runTestCase(testcase);
