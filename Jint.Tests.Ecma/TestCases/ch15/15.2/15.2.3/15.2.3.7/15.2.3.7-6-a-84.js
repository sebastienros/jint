/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-84.js
 * @description Object.defineProperties will not throw TypeError when P.configurable is false, P.writalbe is false, properties.value and P.value are two Objects refer to the same object (8.12.9 step 10.a.ii.1)
 */


function testcase() {

        var obj = {};

        var obj1 = { length: 10 };

        Object.defineProperty(obj, "foo", { 
            value: obj1, 
            writable: false, 
            configurable: false 
        });

        Object.defineProperties(obj, {
            foo: {
                value: obj1
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", obj1, false, false, false);
    }
runTestCase(testcase);
