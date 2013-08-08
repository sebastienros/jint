/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.13/11.13.1/11.13.1-4-30-s.js
 * @description Strict Mode - SyntaxError is thrown if the identifier 'eval' appears as the LeftHandSideExpression (PrimaryExpression) of simple assignment(=) under strict mode
 * @onlyStrict
 */



function testcase() {
        "use strict";
        var blah = eval;
        try {
            eval("(eval) = 20;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError && blah === eval;
        }
}
runTestCase(testcase);