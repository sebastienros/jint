/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-13.js
 * @description Object.defineProperty - Update [[Enumerable]] attribute of 'name' property to false successfully when [[Enumerable]] and [[Configurable]] attributes of 'name' property are true,  the 'desc' is a generic descriptor which only contains [[Enumerable]] attribute as false, 'name' property is an index data property (8.12.9 step 8)
 */


function testcase() {
    
        var obj = {};

        Object.defineProperty(obj, "0", {
            value: 1001,
            writable: true,
            enumerable: true,
            configurable: true
        });

        Object.defineProperty(obj, "0", {
            enumerable: false
        });
        
        return dataPropertyAttributesAreCorrect(obj, "0", 1001, true, false, true);
    }
runTestCase(testcase);
