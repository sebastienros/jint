/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-106.js
 * @description Object.defineProperty - 'name' and 'desc' are data properties, several attributes values of name and desc are different (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { 
            value: 100, 
            writable: true, 
            enumerable: true, 
            configurable: true 
        });
        
        Object.defineProperty(obj, "foo", { 
            value: 200, 
            writable: false, 
            enumerable: false 
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", 200, false, false, true);
    }
runTestCase(testcase);
