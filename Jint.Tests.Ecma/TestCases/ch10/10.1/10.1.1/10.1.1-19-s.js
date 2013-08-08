/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-19-s.js
 * @description Strict Mode - Function code of a FunctionDeclaration contains Use Strict Directive which appears at the start of the block
 * @noStrict
 */


function testcase() {
        function fun() {
            "use strict";
            try {
                eval("var public = 1;");
                return false;
            } catch (e) {
                return e instanceof SyntaxError;
            }
        }
        return fun();
    }
runTestCase(testcase);
