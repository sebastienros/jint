/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-105.js
 * @description Object.defineProperty - 'name' and 'desc' are data properties, name.configurable = true and desc.configurable = false (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", {
            value: 200,
            enumerable: true,
            writable: true,
            configurable: true 
        });

        Object.defineProperty(obj, "foo", {
            configurable: false
        });
        
        return dataPropertyAttributesAreCorrect(obj, "foo", 200, true, true, false);
    }
runTestCase(testcase);
