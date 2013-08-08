/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-16.js
 * @description Object.defineProperties - 'O' is a Number object which implements its own [[GetOwnProperty]] method to get 'P' (8.12.9 step 1 ) 
 */


function testcase() {

        var obj = new Number(-9);

        Object.defineProperty(obj, "prop", {
            value: 11,
            configurable: false
        });

        try {
            Object.defineProperties(obj, {
                prop: {
                    value: 12,
                    configurable: true
                }
            });
            return false;
        } catch (e) {
            return e instanceof TypeError && dataPropertyAttributesAreCorrect(obj, "prop", 11, false, false, false);
        }
    }
runTestCase(testcase);
