/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-72.js
 * @description Object.defineProperties throws TypeError when P is data property and  P.configurable is false, P.writable is false, desc is data property and  desc.value is not equal to P.value (8.12.9 step 10.a.ii.1)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { 
            value: 10, 
            writable: false, 
            configurable: false 
        });

        try {
            Object.defineProperties(obj, {
                foo: {
                    value: 20
                }
            });
            return false;
        } catch (e) {
            return (e instanceof TypeError) && dataPropertyAttributesAreCorrect(obj, "foo", 10, false, false, false);
        }
    }
runTestCase(testcase);
