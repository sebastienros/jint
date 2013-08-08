/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-20-s.js
 * @description Strict Mode - Function code of a FunctionDeclaration contains Use Strict Directive which appears in the middle of the block
 * @noStrict
 */


function testcase() {
        function fun() {
            eval("var public = 1;");
            "use strict";
            return public === 1;
        }
        return fun();
    }
runTestCase(testcase);
