/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 12.6.3; 
 * The production 
 *     IterationStatement : for ( var VariableDeclarationListNoIn ; Expressionopt ; Expressionopt ) Statement
 * is evaluated as follows:
 *
 * @path ch12/12.6/12.6.3/12.6.3_2-3-a-ii-19.js
 * @description The for Statement - (normal, V, empty) will be returned when first Expression is a string (value is 'undefined')
 */


function testcase() {
        var accessed = false;
        for (var i = 0; "undefined";) {
            accessed = true;
            break;
        }
        return accessed;
    }
runTestCase(testcase);
