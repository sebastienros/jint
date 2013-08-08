/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.2/10.4.2-1-3.js
 * @description Indirect call to eval has context set to global context (catch block)
 */

var __10_4_2_1_3 = "str";
function testcase() {

        try {

            var _eval = eval;
            var __10_4_2_1_3 = "str1";
            try {
                throw "error";
            }
            catch (e) {
                var __10_4_2_1_3 = "str2";
                if (_eval("\'str\' === __10_4_2_1_3") === true &&  // indirect eval
                    eval("\'str2\' === __10_4_2_1_3") === true) {  // direct eval
                    return true;
                } else {
                    return false;
                }
            }
        } finally {
            delete this.__10_4_2_1_3;
        }
    }
runTestCase(testcase);
