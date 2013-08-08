/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-15-s.js
 * @description Strict Mode - Function code that is part of a FunctionDeclaration is strict function code if FunctionDeclaration is contained in use strict
 * @noStrict
 */


function testcase() {
        "use strict";
        function fun() {
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
