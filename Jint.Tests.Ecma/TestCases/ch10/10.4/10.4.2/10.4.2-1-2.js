/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.2/10.4.2-1-2.js
 * @description Indirect call to eval has context set to global context (nested function)
 */

var __10_4_2_1_2 = "str";
function testcase() {
        try {

            var _eval = eval;
            var __10_4_2_1_2 = "str1";
            function foo() {
                var __10_4_2_1_2 = "str2";
                if(_eval("\'str\' === __10_4_2_1_2") === true &&  // indirect eval
                    eval("\'str2\' === __10_4_2_1_2") === true) {   // direct eval
                    return true;
                } else {
                    return false;
                }
            }
            return foo();
        } finally {
            delete this.__10_4_2_1_1_2;
        }
    }
runTestCase(testcase);
