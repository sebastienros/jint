/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 12.6.3; 
 * The production 
 *     IterationStatement : for ( var VariableDeclarationListNoIn ; Expressionopt ; Expressionopt ) Statement
 * is evaluated as follows:
 *
 * @path ch12/12.6/12.6.3/12.6.3_2-3-a-ii-2.js
 * @description The for Statement - (normal, V, empty) will be returned when first Expression is a Boolean object
 */


function testcase() {
        var accessed = false;
        var boolObj = new Boolean(false);
        for (var i = 0; boolObj;) {
            accessed = true;
            break;
        }
        return accessed;
    }
runTestCase(testcase);
