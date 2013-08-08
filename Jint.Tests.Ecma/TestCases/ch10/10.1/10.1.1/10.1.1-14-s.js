/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-14-s.js
 * @description Strict Mode - The call to eval function is contained in a Strict Mode block
 * @noStrict
 */


function testcase() {
        'use strict';
        try {
            eval("var public = 1;");
            return false;
        } catch (e) {
            return true;
        }
    }
runTestCase(testcase);
