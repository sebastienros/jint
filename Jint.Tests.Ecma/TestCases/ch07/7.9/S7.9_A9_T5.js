// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Check Do-While Statement for automatic semicolon insertion
 *
 * @path ch07/7.9/S7.9_A9_T5.js
 * @description Execute do { \n ; \n }while((false) \n )
 */

//CHECK#1
do {
  ;
} while ((false) 
)

