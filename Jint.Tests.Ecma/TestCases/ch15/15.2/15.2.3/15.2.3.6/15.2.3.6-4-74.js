/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-74.js
 * @description Object.defineProperty - desc.writable and name.writable are two boolean values with different values (8.12.9 step 6)
 */


function testcase() {
    
        var obj = {};

        Object.defineProperty(obj, "foo", { writable: false, configurable: true });

        Object.defineProperty(obj, "foo", { writable: true });
        return dataPropertyAttributesAreCorrect(obj, "foo", undefined, true, false, true);
    }
runTestCase(testcase);
