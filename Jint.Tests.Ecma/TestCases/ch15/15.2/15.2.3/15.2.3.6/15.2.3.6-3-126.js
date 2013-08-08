/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-126.js
 * @description Object.defineProperty - 'value' property in 'Attributes' is present  (8.10.5 step 5)
 */


function testcase() {
        var obj = {};

        var attr = { value: 100 };

        Object.defineProperty(obj, "property", attr);

        return obj.property === 100;
    }
runTestCase(testcase);
