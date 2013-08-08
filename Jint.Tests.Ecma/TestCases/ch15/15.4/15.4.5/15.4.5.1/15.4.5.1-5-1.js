/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.5/15.4.5.1/15.4.5.1-5-1.js
 * @description Defining a property named 4294967295 (2**32-1)(not an array element)
 */


function testcase() {  
  var a =[];
  a[4294967295] = "not an array element" ;
  return a[4294967295] === "not an array element";
 }
runTestCase(testcase);
