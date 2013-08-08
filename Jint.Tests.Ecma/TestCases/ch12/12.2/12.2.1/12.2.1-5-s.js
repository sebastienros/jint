/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-5-s.js
 * @description Strict Mode - a Function declaring var named 'eval' does not throw SyntaxError
 * @onlyStrict
 */


function testcase() {
        'use strict';
        Function('var eval;');
        return true;
    }
runTestCase(testcase);
