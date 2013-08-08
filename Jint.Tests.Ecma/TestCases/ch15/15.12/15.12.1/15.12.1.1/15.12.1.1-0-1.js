/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-1.js
 * @description The JSON lexical grammar treats whitespace as a token seperator
 */


function testcase() {
  
  try {
    JSON.parse('12\t\r\n 34'); // should produce a syntax error as whitespace results in two tokens
    }
  catch (e) {
      if (e.name === 'SyntaxError') return true;
      }
  }
runTestCase(testcase);
