/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82.js
 * @description Object.defineProperty - desc.configurable and name.configurable are boolean negation of each other (8.12.9 step 6)
 */


function testcase() {
    
        var obj = {};

        Object.defineProperty(obj, "foo", { configurable: true });

        Object.defineProperty(obj, "foo", { configurable: false });
        return dataPropertyAttributesAreCorrect(obj, "foo", undefined, false, false, false);
    }
runTestCase(testcase);
