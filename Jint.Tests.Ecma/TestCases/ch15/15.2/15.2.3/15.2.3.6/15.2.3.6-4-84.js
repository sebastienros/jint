/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-84.js
 * @description Object.defineProperty will not throw TypeError if name.configurable = false, name.writable = false, name.value = null and desc.value = null (8.12.9 step 10.a.ii.1)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { 
            value: null, 
            writable: false, 
            configurable: false 
        });

        Object.defineProperty(obj, "foo", { 
            value: null,  
            writable: false, 
            configurable: false 
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", null, false, false, false);
    }
runTestCase(testcase);
