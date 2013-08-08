/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-4-1.js
 * @description JSON.stringify ignores replacer aruguments that are not functions or arrays..
 */


function testcase() {
  try {
     return JSON.stringify([42],{})=== '[42]';
     }
   catch (e) {return  false}
  }
runTestCase(testcase);
