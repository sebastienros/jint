/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-66-1.js
 * @description Object.defineProperties throws TypeError when P.configurable is false, P.enumerable and desc.enumerable has different values (8.12.9 step 7.b)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", {
            value: 10, 
            enumerable: false, 
            configurable: false 
        });

        try {
            Object.defineProperties(obj, {
                foo: {
                    enumerable: true
                }
            });
            return false;
        } catch (e) {
            return (e instanceof TypeError) && dataPropertyAttributesAreCorrect(obj, "foo", 10, false, false, false);
        }
    }
runTestCase(testcase);
