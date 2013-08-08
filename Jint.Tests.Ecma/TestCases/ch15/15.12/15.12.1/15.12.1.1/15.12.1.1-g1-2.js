/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g1-2.js
 * @description The JSON lexical grammar treats <CR> as a whitespace character
 */


function testcase() {
  if (JSON.parse('\r1234')!==1234) return false; // <cr> should be ignored
  try {
    JSON.parse('12\r34'); // <CR> should produce a syntax error as whitespace results in two tokens
    }
  catch (e) {
      if (e.name === 'SyntaxError') return true;
      }
  }
runTestCase(testcase);
